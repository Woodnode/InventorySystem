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
  projectCode: z.string().nullable().optional(),
  collection: z.string().nullable().optional(),
  volumeNumber: z.string().nullable().optional(),
  productType: z.string().nullable().optional(),
  year: z.number().int().nullable().optional(),
  weightPerCopyLb: z.number().nullable().optional(),
  company: z.string().nullable().optional(),
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
  
  projectCode: z.string().max(200).optional(),
  collection: z.string().max(150).optional(),
  volumeNumber: z.string().max(50).optional(),
  productType: z.string().max(100).optional(),
  year: z.number().int().optional(),
  weightPerCopyLb: z.number().optional(),
  company: z.string().max(150).optional(),

  section: z.string().max(50).optional(),
  space: z.string().max(50).optional(),
  pallet: z.string().max(50).optional(),
  boxesCount: z.number().int().min(0),
  copiesPerBox: z.number().int().min(0),
  entryDate: z.string().datetime().optional(),
  exitDate: z.string().datetime().optional(),
  distributorName: z.string().max(150).optional(),
  returnDate: z.string().datetime().optional(),
  comment: z.string().max(1000).optional(),
});
export type CreateProductInput = z.infer<typeof createProductSchema>;
