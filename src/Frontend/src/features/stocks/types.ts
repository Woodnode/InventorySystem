import { z } from 'zod';

/** Schéma aligné sur StockDto côté backend. */
export const stockSchema = z.object({
  productId: z.string().uuid(),
  warehouseId: z.string().uuid(),
  quantity: z.number().int(),
});
export type Stock = z.infer<typeof stockSchema>;
