import { useEffect, useState, type ReactNode } from 'react';
import { apiClient, ensureFreshAccessToken } from '../../shared/api-client/client';
import {
  clearAuth,
  getAuth,
  hasAtLeastRole as hasAtLeastRoleFromStorage,
  setAuth,
  subscribe,
  type StoredAuth,
} from '../../shared/auth/tokenStorage';
import { authResultSchema, type LoginInput, type RegisterInput } from './types';
import { AuthContext } from './useAuth';

/**
 * État d'authentification global. S'abonne à tokenStorage (source de vérité partagée
 * avec l'intercepteur axios, hors arbre React — voir shared/auth/tokenStorage.ts) pour
 * re-render automatiquement après un login/logout/refresh silencieux.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuthState] = useState<StoredAuth | null>(getAuth());
  // L'access token ne survit jamais à un rechargement de page (voir tokenStorage.ts) :
  // s'il reste un refresh token mais pas d'access token, la session existe mais n'est
  // pas encore utilisable — le temps d'un refresh silencieux avant de rendre les pages
  // protégées (sinon des composants comme useLowStockAlerts démarreraient sans token).
  const [isInitializing, setIsInitializing] = useState(() => !!auth?.refreshToken && !auth.accessToken);

  useEffect(() => subscribe(setAuthState), []);

  useEffect(() => {
    if (!isInitializing) return;
    ensureFreshAccessToken()
      .catch(() => clearAuth())
      .finally(() => setIsInitializing(false));
  }, [isInitializing]);

  async function login(input: LoginInput): Promise<void> {
    const { data } = await apiClient.post('/auth/login', input);
    setAuth(authResultSchema.parse(data));
  }

  async function register(input: RegisterInput): Promise<void> {
    // Auto-inscription toujours en rôle Employe — voir AuthController côté backend :
    // seul un Admin déjà connecté peut créer un compte Gestionnaire/Admin.
    const { data } = await apiClient.post('/auth/register', { ...input, role: 'Employe' });
    setAuth(authResultSchema.parse(data));
  }

  function logout(): void {
    clearAuth();
  }

  return (
    <AuthContext.Provider
      value={{
        auth,
        isAuthenticated: auth !== null,
        isInitializing,
        login,
        register,
        logout,
        hasAtLeastRole: hasAtLeastRoleFromStorage,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
