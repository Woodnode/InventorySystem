import { z } from 'zod';

/** Schéma Zod aligné sur ProductDto côté backend (source de vérité partagée). */
export const productSchema = z.object({
  id: z.string().uuid(),
  sku: z.string(),
  name: z.string(),
  description: z.string().nullable(),
  quantity: z.number().int(), // total agrégé, tous entrepôts confondus (voir plan §3)
  lowStockThreshold: z.number().int(),
  isLowOnStock: z.boolean(),
});
export type Product = z.infer<typeof productSchema>;

/**
 * Schéma du formulaire de création, aligné sur CreateProductCommand.
 * Un produit est toujours créé avec un stock initial dans un entrepôt existant
 * (voir plan §3 : pas de produit "flottant" sans localisation physique).
 */
export const createProductSchema = z.object({
  sku: z.string().min(3).max(32),
  name: z.string().min(1).max(200),
  description: z.string().max(1000).optional(),
  lowStockThreshold: z.number().int().min(0),
  warehouseId: z.string().uuid('Choisis un entrepôt'),
  initialQuantity: z.number().int().min(0),
  supplierId: z.string().uuid().optional().or(z.literal('')),
});
export type CreateProductInput = z.infer<typeof createProductSchema>;
