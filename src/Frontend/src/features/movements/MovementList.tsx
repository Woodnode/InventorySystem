import { DataList } from '../../shared/components/DataList';
import { formatMovementType, warehouseNameLookup } from '../../shared/utils/movementLabels';
import type { Warehouse } from '../warehouses/types';
import type { StockMovement } from './types';

/**
 * Historique des mouvements — identique sur ProductDetailPage et MovementsPage (seule la
 * façon dont on arrive au produit sélectionné diffère entre les deux écrans).
 */
export function MovementList({
  movements,
  warehouses,
  className,
}: {
  movements: StockMovement[];
  warehouses: Warehouse[] | undefined;
  className?: string;
}) {
  const warehouseName = warehouseNameLookup(warehouses);

  return (
    <DataList
      items={movements}
      keyOf={(m) => m.id}
      className={className}
      itemClassName="text-sm"
      renderItem={(m) => (
        <>
          <div>
            <span className="font-medium text-slate-800">{formatMovementType(m.type)}</span>
            <span className="ml-2 text-slate-500">
              {warehouseName(m.warehouseId)}
              {m.toWarehouseId && ` → ${warehouseName(m.toWarehouseId)}`}
            </span>
            {m.reason && <span className="ml-2 text-slate-400">({m.reason})</span>}
          </div>
          <div className="flex items-center gap-3">
            <span className="font-mono text-slate-600">{m.quantity}</span>
            <span className="text-xs text-slate-400">
              {new Date(m.createdAtUtc).toLocaleString('fr-CA')}
            </span>
          </div>
        </>
      )}
    />
  );
}
