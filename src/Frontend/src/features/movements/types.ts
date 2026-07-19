import { z } from 'zod';

/** Aligné sur l'enum MovementType côté backend (Domain.Entities.MovementType). */
export const movementTypeSchema = z.enum(['In', 'Out', 'Transfer']);
export type MovementType = z.infer<typeof movementTypeSchema>;

/** Schéma aligné sur StockMovementDto côté backend. */
export const stockMovementSchema = z.object({
  id: z.string().uuid(),
  productId: z.string().uuid(),
  warehouseId: z.string().uuid(),
  toWarehouseId: z.string().uuid().nullable(),
  type: movementTypeSchema,
  quantity: z.number().int(),
  reason: z.string().nullable(),
  createdAtUtc: z.string(),
});
export type StockMovement = z.infer<typeof stockMovementSchema>;

/** Schéma du formulaire, aligné sur RecordMovementCommand. */
export const recordMovementSchema = z
  .object({
    productId: z.string().uuid('Choisis un produit'),
    warehouseId: z.string().uuid('Choisis un entrepôt'),
    type: movementTypeSchema,
    quantity: z.number().int().positive('La quantité doit être positive'),
    reason: z.string().max(500).optional(),
    toWarehouseId: z.string().uuid().optional().or(z.literal('')),
  })
  .refine((v) => v.type !== 'Transfer' || !!v.toWarehouseId, {
    message: "L'entrepôt de destination est requis pour un transfert",
    path: ['toWarehouseId'],
  })
  .refine((v) => v.type !== 'Transfer' || v.toWarehouseId !== v.warehouseId, {
    message: "L'entrepôt de destination doit être différent de l'entrepôt source",
    path: ['toWarehouseId'],
  });
export type RecordMovementInput = z.infer<typeof recordMovementSchema>;
