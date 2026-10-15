import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  AUTH_REQUEST_CONFIG,
  apiClient,
  ensureFreshAccessToken,
  revokeSession,
} from '../../shared/api-client/client';
import {
  clearAuth,
  getAuth,
  hasAtLeastRole as hasAtLeastRoleFromStorage,
  hasSessionHint,
  setAuth,
  subscribe,
  type StoredAuth,
} from '../../shared/auth/tokenStorage';
import { authResultSchema, type LoginInput, type RegisterInput } from './types';
import { AuthContext } from './useAuth';

/**
 * État d'authentification global. Un seul chemin de refresh silencieux :
 * `isInitializing` passe à true au boot (session ouverte avant le rechargement) ou quand
 * un autre onglet vient de se connecter. Le refresh token reste dans son cookie httpOnly,
 * ce composant ne le manipule jamais.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuthState] = useState<StoredAuth | null>(getAuth());
  const [isInitializing, setIsInitializing] = useState(() => !getAuth() && hasSessionHint());

  useEffect(() => {
    return subscribe((next) => {
      setAuthState(next);
      // Multi-onglets : un autre onglet s'est connecté, cet onglet n'a pas encore d'access token.
      if (!next && hasSessionHint()) {
        setIsInitializing(true);
      }
    });
  }, []);

  useEffect(() => {
    if (!isInitializing) return;

    let cancelled = false;
    ensureFreshAccessToken()
      .catch(() => {
        if (!cancelled) clearAuth();
      })
      .finally(() => {
        if (!cancelled) setIsInitializing(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isInitializing]);

  const login = useCallback(async (input: LoginInput): Promise<void> => {
    const { data } = await apiClient.post('/auth/login', input, AUTH_REQUEST_CONFIG);
    setAuth(authResultSchema.parse(data));
  }, []);

  const register = useCallback(async (input: RegisterInput): Promise<void> => {
    const { data } = await apiClient.post(
      '/auth/register',
      { ...input, role: 'Employe' },
      AUTH_REQUEST_CONFIG,
    );
    setAuth(authResultSchema.parse(data));
  }, []);

  const logout = useCallback((): void => {
    clearAuth();
    void revokeSession();
  }, []);

  const value = useMemo(
    () => ({
      auth,
      isAuthenticated: auth !== null,
      isInitializing,
      login,
      register,
      logout,
      hasAtLeastRole: hasAtLeastRoleFromStorage,
    }),
    [auth, isInitializing, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
