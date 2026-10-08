import type { ReactNode } from 'react';
import { getErrorMessage } from '../api-client/errorMessage';

/** Bandes grises reprenant la forme d'une ligne de liste, le temps du chargement. */
function Skeleton({ rows }: { rows: number }) {
  return (
    <ul className="surface mt-6 divide-y divide-slate-100" aria-hidden="true">
      {Array.from({ length: rows }).map((_, i) => (
        <li key={i} className="flex items-center justify-between gap-4 px-4 py-4">
          <div className="min-w-0 flex-1 space-y-2">
            <div className="h-4 w-1/2 animate-pulse rounded bg-slate-200" />
            <div className="h-3 w-1/4 animate-pulse rounded bg-slate-100" />
          </div>
          <div className="h-6 w-20 shrink-0 animate-pulse rounded bg-slate-100" />
        </li>
      ))}
    </ul>
  );
}

/**
 * Bloc chargement/erreur/vide standard pour une page alimentée par TanStack Query
 * (pattern d'origine : DashboardPage). Ne rend rien si aucun état particulier ne
 * s'applique — la page affiche alors son contenu normal juste en dessous.
 *
 * Le conteneur porte `aria-live` : les trois états se remplaçaient auparavant en
 * silence, sans qu'un lecteur d'écran signale l'arrivée des données ou l'échec.
 */
export function QueryState({
  isLoading,
  isError,
  error,
  isEmpty,
  emptyMessage,
  emptyAction,
  skeletonRows = 4,
  className = 'mt-6',
}: {
  isLoading: boolean;
  isError: boolean;
  error?: unknown;
  isEmpty?: boolean;
  emptyMessage?: string;
  /** Bouton ou lien proposé quand la liste est vide (« comment la remplir »). */
  emptyAction?: ReactNode;
  skeletonRows?: number;
  className?: string;
}) {
  if (isLoading) {
    return (
      <div role="status" aria-live="polite" className={className}>
        <span className="sr-only">Chargement en cours…</span>
        <Skeleton rows={skeletonRows} />
      </div>
    );
  }

  if (isError) {
    return (
      <div role="alert" className={`${className} surface border-rose-200 bg-rose-50 px-4 py-3`}>
        <p className="text-sm font-medium text-rose-800">
          {getErrorMessage(error, 'Impossible de charger les données.')}
        </p>
        <button
          type="button"
          className="mt-2 text-sm font-semibold text-rose-800 underline underline-offset-2"
          onClick={() => window.location.reload()}
        >
          Réessayer
        </button>
      </div>
    );
  }

  if (isEmpty && emptyMessage) {
    return (
      <div
        role="status"
        aria-live="polite"
        className={`${className} surface flex flex-col items-center gap-3 px-4 py-10 text-center`}
      >
        {/* text-slate-600 : 7,0:1. L'ancien text-slate-400 plafonnait à 2,6:1. */}
        <p className="text-sm text-slate-600">{emptyMessage}</p>
        {emptyAction}
      </div>
    );
  }

  return null;
}
