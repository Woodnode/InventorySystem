import type { Warehouse } from '../../features/warehouses/types';
import type { MovementType } from '../../features/movements/types';

/** Libellé FR d'un type de mouvement (ProductDetailPage, MovementsPage). */
export function formatMovementType(type: MovementType): string {
  switch (type) {
    case 'In':
      return 'Entrée';
    case 'Out':
      return 'Sortie';
    case 'Transfer':
      return 'Transfert';
  }
}

/** Résout un id d'entrepôt en nom affichable, avec repli sur l'id si la liste n'est pas chargée. */
export function warehouseNameLookup(warehouses: Warehouse[] | undefined) {
  return (warehouseId: string) => warehouses?.find((w) => w.id === warehouseId)?.name ?? warehouseId;
}
