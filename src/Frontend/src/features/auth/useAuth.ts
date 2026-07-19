import { createContext, useContext } from 'react';
import type { StoredAuth } from '../../shared/auth/tokenStorage';
import type { LoginInput, RegisterInput } from './types';

export interface AuthContextValue {
  auth: StoredAuth | null;
  isAuthenticated: boolean;
  isInitializing: boolean;
  login: (input: LoginInput) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => void;
  hasAtLeastRole: (role: 'Employe' | 'Gestionnaire' | 'Admin') => boolean;
}

// Séparé d'AuthContext.tsx (qui ne garde que le composant AuthProvider) : le Fast Refresh
// de Vite n'accepte que des fichiers n'exportant QUE des composants, pas un mélange
// composant + hook + contexte.
export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth doit être utilisé sous <AuthProvider>.');
  return ctx;
}
