import { z } from 'zod';

/** Aligné sur DailyFlowDto côté backend. */
export const dailyFlowSchema = z.object({
  date: z.string(),
  in: z.number().int(),
  out: z.number().int(),
  net: z.number().int(),
});

export const warehouseLoadSchema = z.object({
  warehouseId: z.string(),
  name: z.string(),
  productCount: z.number().int(),
  totalQuantity: z.number().int(),
});

export const collectionCountSchema = z.object({
  collection: z.string(),
  count: z.number().int(),
});

/** Aligné sur DashboardStatsDto côté backend (source de vérité partagée). */
export const dashboardStatsSchema = z.object({
  totalProducts: z.number().int(),
  totalWeightGrams: z.number(),
  activeWarehouses: z.number().int(),
  inactiveWarehouses: z.number().int(),
  topCollections: collectionCountSchema.array(),
  warehouseLoads: warehouseLoadSchema.array(),
  dailyFlow: dailyFlowSchema.array(),
  netCurrentPeriod: z.number().int(),
  netPreviousPeriod: z.number().int(),
  // null quand la période précédente est à zéro : aucune variation n'est calculable.
  changePercent: z.number().nullable(),
  periodDays: z.number().int(),
});

export type DailyFlow = z.infer<typeof dailyFlowSchema>;
export type DashboardStats = z.infer<typeof dashboardStatsSchema>;
export type WarehouseLoad = z.infer<typeof warehouseLoadSchema>;
