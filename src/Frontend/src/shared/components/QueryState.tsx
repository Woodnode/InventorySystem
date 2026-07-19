import { getErrorMessage } from '../api-client/errorMessage';

/**
 * Bloc loading/erreur/vide standard pour une page alimentée par TanStack Query
 * (pattern d'origine : DashboardPage). Ne rend rien si aucun état particulier ne
 * s'applique — la page affiche alors son contenu normal juste en dessous.
 */
export function QueryState({
  isLoading,
  isError,
  error,
  isEmpty,
  emptyMessage,
  className = 'mt-6',
}: {
  isLoading: boolean;
  isError: boolean;
  error?: unknown;
  isEmpty?: boolean;
  emptyMessage?: string;
  className?: string;
}) {
  if (isLoading) return <p className={`${className} text-slate-400`}>Chargement…</p>;

  if (isError) {
    return (
      <p className={`${className} text-rose-600`}>
        {getErrorMessage(error, 'Impossible de charger les données. Réessaie.')}
      </p>
    );
  }

  if (isEmpty && emptyMessage) return <p className={`${className} text-slate-400`}>{emptyMessage}</p>;

  return null;
}
