import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { apiClient } from '../../shared/api-client/client';
import { movementTypeSchema } from './types';

/** Aligné sur RecentMovementDto côté backend. */
export const recentMovementSchema = z.object({
  id: z.string().uuid(),
  productId: z.string().uuid(),
  productSku: z.string(),
  productName: z.string(),
  warehouseName: z.string(),
  toWarehouseName: z.string().nullable(),
  type: movementTypeSchema,
  quantity: z.number().int(),
  reason: z.string().nullable(),
  createdAtUtc: z.string(),
});

export type RecentMovement = z.infer<typeof recentMovementSchema>;

/**
 * Derniers mouvements tous produits confondus.
 *
 * L'écran restait vide tant qu'aucun produit n'était choisi, alors que la question
 * la plus fréquente est « que s'est-il passé récemment ? ».
 */
export function useRecentMovements(take = 15) {
  return useQuery<RecentMovement[]>({
    queryKey: ['movements', 'recent', take],
    queryFn: async () => {
      const { data } = await apiClient.get('/movements/recent', { params: { take } });
      return recentMovementSchema.array().parse(data);
    },
  });
}
