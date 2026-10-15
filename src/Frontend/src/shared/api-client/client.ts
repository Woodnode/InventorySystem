import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { clearAuth, getAuth, setAuth, type StoredAuth } from '../auth/tokenStorage';

/**
 * Client HTTP typé consommant la même API REST ASP.NET Core que l'app mobile.
 * L'intercepteur attache le JWT et gère le refresh automatique sur 401 (voir plan §7).
 */
let apiUrl = import.meta.env.VITE_API_URL ?? '/api/v1';
if (window.location.hostname === '10.0.2.2') {
  apiUrl = 'http://10.0.2.2:5244/api/v1';
}

export const apiClient = axios.create({
  baseURL: apiUrl,
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

/**
 * Options communes aux appels d'authentification : le navigateur doit joindre et accepter
 * le cookie httpOnly du refresh token, et l'API doit savoir que ce client le veut en cookie
 * plutôt que dans le corps de la réponse (le client mobile, lui, n'envoie pas l'en-tête).
 */
export const AUTH_REQUEST_CONFIG = {
  withCredentials: true,
  headers: { 'X-Refresh-Token-Transport': 'cookie' },
} as const;

async function requestNewAccessToken(): Promise<StoredAuth> {
  // Corps vide : le refresh token part dans le cookie, que ce script ne voit jamais.
  const { data } = await axios.post<StoredAuth>(
    `${apiClient.defaults.baseURL}/auth/refresh`,
    undefined,
    AUTH_REQUEST_CONFIG,
  );
  setAuth(data);
  return data;
}

/**
 * Révoque la session côté serveur et efface le cookie. Best effort : la session locale est
 * fermée quoi qu'il arrive, un échec réseau ne doit pas bloquer la déconnexion.
 */
export async function revokeSession(): Promise<void> {
  try {
    await axios.post(`${apiClient.defaults.baseURL}/auth/logout`, undefined, AUTH_REQUEST_CONFIG);
  } catch {
    // Le jeton expirera de lui-même ; rien d'autre à faire côté client.
  }
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

// Liste explicite plutôt qu'un match par sous-chaîne sur '/auth/' : un futur endpoint
// protégé nommé sous /auth/* (ex. /auth/me, /auth/change-password) doit continuer à
// bénéficier du refresh-and-retry silencieux, pas être traité comme login/register/refresh
// eux-mêmes (voir ré-audit).
const AUTH_ENDPOINTS_WITHOUT_RETRY = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetryableConfig | undefined;
    const isAuthEndpoint = AUTH_ENDPOINTS_WITHOUT_RETRY.some((path) => config?.url?.includes(path));

    if (error.response?.status !== 401 || !config || config._retried || isAuthEndpoint) {
      // Un 401 sur /auth/login|register|refresh n'est jamais une session expirée : c'est
      // un mauvais mot de passe ou un refresh token invalide. On laisse l'appelant (le
      // formulaire) gérer l'erreur lui-même plutôt que de forcer une redirection.
      if (error.response?.status === 401 && !isAuthEndpoint) {
        // Refresh déjà tenté sur un endpoint protégé : session vraiment expirée.
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
