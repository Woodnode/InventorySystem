import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { clearAuth, getAuth, setAuth, type StoredAuth } from '../auth/tokenStorage';

/**
 * Client HTTP typé consommant la même API REST ASP.NET Core que l'app mobile.
 * L'intercepteur attache le JWT et gère le refresh automatique sur 401 (voir plan §7).
 */
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'https://localhost:7296/api/v1',
  headers: { 'Content-Type': 'application/json' },
});

// Attache le token d'accès à chaque requête.
apiClient.interceptors.request.use((config) => {
  const auth = getAuth();
  if (auth?.accessToken) {
    config.headers.Authorization = `Bearer ${auth.accessToken}`;
  }
  return config;
});

async function requestNewAccessToken(): Promise<StoredAuth> {
  const auth = getAuth();
  if (!auth?.refreshToken) throw new Error('Aucun refresh token disponible.');

  const { data } = await axios.post<StoredAuth>(
    `${apiClient.defaults.baseURL}/auth/refresh`,
    { refreshToken: auth.refreshToken },
  );
  setAuth(data);
  return data;
}

// Un seul refresh en vol à la fois : si plusieurs requêtes échouent en 401 simultanément
// (ex. plusieurs widgets du dashboard), elles partagent la même tentative de refresh
// plutôt que d'en déclencher une par requête.
let refreshPromise: Promise<StoredAuth> | null = null;

/**
 * Point d'entrée unique pour obtenir un access token frais, partagé par l'intercepteur
 * 401 ci-dessous ET par AuthProvider au montage (l'access token ne survit jamais à un
 * rechargement de page — voir tokenStorage.ts — donc AuthProvider doit en redemander un
 * avant de rendre les pages protégées). Le single-flight évite qu'un refresh déclenché
 * par AuthProvider et un autre déclenché par une requête concurrente ne s'exécutent en
 * double.
 */
export function ensureFreshAccessToken(): Promise<StoredAuth> {
  refreshPromise ??= requestNewAccessToken().finally(() => {
    refreshPromise = null;
  });
  return refreshPromise;
}

interface RetryableConfig extends InternalAxiosRequestConfig {
  _retried?: boolean;
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetryableConfig | undefined;
    const isAuthEndpoint = config?.url?.includes('/auth/');

    if (error.response?.status !== 401 || !config || config._retried || isAuthEndpoint) {
      if (error.response?.status === 401) {
        // Refresh déjà tenté (ou endpoint d'auth lui-même) : session vraiment expirée.
        clearAuth();
        window.location.assign('/login');
      }
      return Promise.reject(error);
    }

    config._retried = true;

    try {
      const refreshed = await ensureFreshAccessToken();

      config.headers.Authorization = `Bearer ${refreshed.accessToken}`;
      return apiClient(config);
    } catch {
      clearAuth();
      window.location.assign('/login');
      return Promise.reject(error);
    }
  },
);
