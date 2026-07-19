import { useQuery } from '@tanstack/react-query';
import { apiClient } from '../../shared/api-client/client';
import { stockSchema, type Stock } from './types';

/** Répartition du stock d'un produit, entrepôt par entrepôt. */
export function useStockByProduct(productId: string | undefined) {
  return useQuery<Stock[]>({
    queryKey: ['stocks', 'product', productId],
    queryFn: async () => {
      const { data } = await apiClient.get(`/stocks/product/${productId}`);
      return stockSchema.array().parse(data);
    },
    enabled: !!productId,
  });
}
