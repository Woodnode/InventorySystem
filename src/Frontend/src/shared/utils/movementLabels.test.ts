import { describe, expect, it } from 'vitest';
import type { Warehouse } from '../../features/warehouses/types';
import { formatMovementType, warehouseNameLookup } from './movementLabels';

describe('formatMovementType', () => {
  it.each([
    ['In', 'Entrée'],
    ['Out', 'Sortie'],
    ['Transfer', 'Transfert'],
  ] as const)('formats %s as %s', (type, expected) => {
    expect(formatMovementType(type)).toBe(expected);
  });
});

describe('warehouseNameLookup', () => {
  const warehouses: Warehouse[] = [
    { id: 'w1', name: 'Entrepôt Nord', address: null, isActive: true },
    { id: 'w2', name: 'Entrepôt Sud', address: null, isActive: true },
  ];

  it('resolves a known warehouse id to its name', () => {
    const lookup = warehouseNameLookup(warehouses);

    expect(lookup('w2')).toBe('Entrepôt Sud');
  });

  it('falls back to the id itself when the warehouse is not in the list', () => {
    const lookup = warehouseNameLookup(warehouses);

    expect(lookup('unknown-id')).toBe('unknown-id');
  });

  it('falls back to the id when the warehouse list is not loaded yet', () => {
    const lookup = warehouseNameLookup(undefined);

    expect(lookup('w1')).toBe('w1');
  });
});
