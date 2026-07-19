import type { ReactNode } from 'react';
import { useAuth } from '../features/auth/useAuth';

/**
 * Masque son contenu si l'utilisateur n'a pas au moins le rôle demandé.
 * Rappel : c'est du confort UX, pas de la sécurité — l'API revalide tout côté serveur
 * via les policies (RequireGestionnaireOrAbove, etc., voir plan §6).
 */
export function RequireRole({
  role,
  fallback = null,
  children,
}: {
  role: 'Employe' | 'Gestionnaire' | 'Admin';
  fallback?: ReactNode;
  children: ReactNode;
}) {
  const { hasAtLeastRole } = useAuth();
  return hasAtLeastRole(role) ? <>{children}</> : <>{fallback}</>;
}
