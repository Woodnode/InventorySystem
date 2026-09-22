import { z } from 'zod';

/** Schéma aligné sur SupplierDto côté backend. */
export const supplierSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  contactEmail: z.string().nullable(),
  phone: z.string().nullable(),
  isActive: z.boolean(),
});
export type Supplier = z.infer<typeof supplierSchema>;

/** Schéma du formulaire de création, aligné sur CreateSupplierCommand. */
export const createSupplierSchema = z.object({
  name: z.string().min(1, 'Le nom est requis').max(200),
  contactEmail: z.string().email('Email invalide').optional().or(z.literal('')),
  phone: z.string().max(30).optional(),
});
export type CreateSupplierInput = z.infer<typeof createSupplierSchema>;
