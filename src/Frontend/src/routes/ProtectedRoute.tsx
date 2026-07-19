import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../features/auth/useAuth';

/** Route guard : redirige vers /login si non authentifié, en mémorisant la page visée. */
export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isInitializing } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  // Session détectée (refresh token persisté) mais pas encore d'access token en mémoire
  // (voir AuthContext.tsx) — le rendu attend la fin du refresh silencieux plutôt que de
  // monter des pages/hooks (ex. SignalR) qui auraient besoin d'un token dès leur montage.
  if (isInitializing) {
    return <p className="mx-auto max-w-5xl px-6 py-10 text-slate-400">Chargement…</p>;
  }

  return <>{children}</>;
}
