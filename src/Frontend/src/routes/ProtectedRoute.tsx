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
    return (
      <div role="status" aria-busy="true" aria-live="polite" className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
        <span className="sr-only">Chargement…</span>
        <div className="h-8 w-48 animate-pulse rounded bg-slate-200" />
        <div className="mt-6 space-y-3">
          <div className="h-16 animate-pulse rounded-card bg-slate-200" />
          <div className="h-16 animate-pulse rounded-card bg-slate-200" />
          <div className="h-16 animate-pulse rounded-card bg-slate-200" />
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
