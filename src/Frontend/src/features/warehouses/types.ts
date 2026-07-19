import { z } from 'zod';

/** Schéma aligné sur WarehouseDto côté backend. */
export const warehouseSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  address: z.string().nullable(),
  isActive: z.boolean(),
});
export type Warehouse = z.infer<typeof warehouseSchema>;

/** Schéma du formulaire de création, aligné sur CreateWarehouseCommand. */
export const createWarehouseSchema = z.object({
  name: z.string().min(1, 'Le nom est requis').max(200),
  address: z.string().max(500).optional(),
});
export type CreateWarehouseInput = z.infer<typeof createWarehouseSchema>;
