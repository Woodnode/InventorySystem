import { Link } from 'react-router-dom';
import {
  ArrowLineDown,
  ArrowLineUp,
  ArrowsLeftRight,
  ClockCounterClockwise,
  type Icon,
} from '@phosphor-icons/react';
import { QueryState } from '../../shared/components/QueryState';
import { formatDepuis } from '../../shared/utils/formatDepuis';
import { formatMovementType } from '../../shared/utils/movementLabels';
import { BadgeMouvement } from '../movements/BadgeMouvement';
import type { RecentMovement } from '../movements/useRecentMovements';
import type { MovementType } from '../movements/types';

/** Une icône par sens de mouvement, pour situer la ligne avant de la lire. Les
    teintes reprennent celles des pastilles, variante sombre comprise. */
const ICONES: Record<MovementType, { icone: Icon; pastille: string }> = {
  In: { icone: ArrowLineDown, pastille: 'badge-marque' },
  Out: { icone: ArrowLineUp, pastille: 'badge-alerte' },
  Transfer: { icone: ArrowsLeftRight, pastille: 'badge-neutre' },
};

/**
 * Derniers mouvements, tous produits confondus.
 *
 * L'accueil disait ce qui manque (stock bas) mais pas ce qui venait de se passer :
 * il fallait ouvrir Mouvements pour savoir si la réception du matin était saisie.
 */
export function ActiviteRecente({
  mouvements,
  isLoading,
  isError,
}: {
  mouvements: RecentMovement[] | undefined;
  isLoading: boolean;
  isError: boolean;
}) {
  return (
    <section className="dashboard-card sm:col-span-2 sm:self-stretch">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-field bg-brand-900/10">
            <ClockCounterClockwise size={22} weight="duotone" className="text-brand-900" aria-hidden="true" />
          </span>
          <h2 className="text-xl font-bold text-slate-800">Activité récente</h2>
        </div>
        <Link
          to="/movements"
          className="lien-marque inline-flex min-h-11 items-center text-sm font-medium text-brand-900 underline-offset-2 hover:underline"
        >
          Tout voir
        </Link>
      </div>

      <QueryState
        isLoading={isLoading}
        isError={isError}
        isEmpty={mouvements?.length === 0}
        emptyTitle="Aucun mouvement"
        emptyIcon={ArrowsLeftRight}
        emptyMessage="Les entrées, sorties et transferts apparaîtront ici dès le premier enregistrement."
        className="mt-4"
        skeletonRows={3}
      />

      {mouvements && mouvements.length > 0 && (
        <ul className="mt-4 space-y-1">
          {mouvements.map((m) => {
            const { icone: Icone, pastille } = ICONES[m.type];
            return (
              <li key={m.id}>
                <Link
                  to={`/products/${m.productId}`}
                  className="group flex items-center gap-3 rounded-field px-2 py-2 transition-colors hover:bg-slate-50"
                >
                  <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-field ${pastille}`}>
                    <Icone size={18} weight="bold" aria-hidden="true" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span
                      className="block truncate text-sm font-semibold text-slate-800 group-hover:text-brand-900"
                      title={m.productName}
                    >
                      {m.productName}
                    </span>
                    <span className="block truncate text-xs text-slate-600">
                      {formatMovementType(m.type)} · {m.warehouseName}
                      {m.toWarehouseName && ` → ${m.toWarehouseName}`}
                    </span>
                  </span>
                  <span className="flex shrink-0 flex-col items-end gap-1">
                    <BadgeMouvement type={m.type} quantite={m.quantity} />
                    <time
                      dateTime={m.createdAtUtc}
                      title={new Date(m.createdAtUtc).toLocaleString('fr-CA')}
                      className="text-xs text-slate-600"
                    >
                      {formatDepuis(m.createdAtUtc)}
                    </time>
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
