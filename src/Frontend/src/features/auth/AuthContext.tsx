import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
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
 * État d'authentification global. Un seul chemin de refresh silencieux :
 * `isInitializing` passe à true au boot ou quand un autre onglet laisse accessToken vide.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuthState] = useState<StoredAuth | null>(getAuth());
  const [isInitializing, setIsInitializing] = useState(
    () => !!getAuth()?.refreshToken && !getAuth()?.accessToken,
  );

  useEffect(() => {
    return subscribe((next) => {
      setAuthState(next);
      // Multi-onglets / storage : refresh token présent, access encore vide → relancer le boot refresh.
      if (next?.refreshToken && !next.accessToken) {
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
    const { data } = await apiClient.post('/auth/login', input);
    setAuth(authResultSchema.parse(data));
  }, []);

  const register = useCallback(async (input: RegisterInput): Promise<void> => {
    const { data } = await apiClient.post('/auth/register', { ...input, role: 'Employe' });
    setAuth(authResultSchema.parse(data));
  }, []);

  const logout = useCallback((): void => {
    clearAuth();
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
