import { z } from 'zod';

/** Schéma aligné sur StockDto côté backend. */
export const stockSchema = z.object({
  productId: z.string().uuid(),
  warehouseId: z.string().uuid(),
  quantity: z.number().int(),
  section: z.string().nullable().optional(),
  space: z.string().nullable().optional(),
  pallet: z.string().nullable().optional(),
  boxesCount: z.number().int().optional(),
  copiesPerBox: z.number().int().optional(),
  entryDate: z.string().nullable().optional(),
  exitDate: z.string().nullable().optional(),
  distributorName: z.string().nullable().optional(),
  returnDate: z.string().nullable().optional(),
  comment: z.string().nullable().optional(),
  inventoryDate: z.string().nullable().optional(),
  responsibleName: z.string().nullable().optional(),
});
export type Stock = z.infer<typeof stockSchema>;
