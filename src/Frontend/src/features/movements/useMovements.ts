import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../shared/api-client/client';
import { pagedResultSchema, type PagedResult } from '../../shared/api-client/pagination';
import { downloadBlob, extractFilename } from '../../shared/utils/downloadBlob';
import { stockMovementSchema, type RecordMovementInput, type StockMovement } from './types';

const PAGE_SIZE = 20;

/** Historique paginé des mouvements d'un produit (plus récents en premier — voir backend,
 * l'historique grandit sans borne). */
export function useMovementsByProduct(productId: string | undefined, page = 1) {
  return useQuery<PagedResult<StockMovement>>({
    queryKey: ['movements', 'product', productId, page],
    queryFn: async () => {
      const { data } = await apiClient.get(`/movements/product/${productId}`, {
        params: { page, pageSize: PAGE_SIZE },
      });
      return pagedResultSchema(stockMovementSchema).parse(data);
    },
    enabled: !!productId,
  });
}

/**
 * Télécharge l'historique des mouvements en CSV ou Excel — pour un produit donné
 * (`productId` renseigné) ou l'historique complet sinon (voir plan §13, export différé).
 */
export async function exportMovements(format: 'Csv' | 'Excel', productId?: string): Promise<void> {
  const response = await apiClient.get('/movements/export', {
    params: { format, productId },
    responseType: 'blob',
  });
  const filename = extractFilename(
    response.headers['content-disposition'] as string | undefined,
    format === 'Csv' ? 'mouvements.csv' : 'mouvements.xlsx',
  );
  downloadBlob(response.data as Blob, filename);
}

export function useRecordMovement() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (input: RecordMovementInput) => {
      const payload = {
        ...input,
        toWarehouseId: input.type === 'Transfer' ? input.toWarehouseId : null,
        clientGuid: crypto.randomUUID(),
      };
      const { data } = await apiClient.post<{ id: string }>('/movements', payload);
      return data;
    },
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['movements', 'product', variables.productId] });
      void queryClient.invalidateQueries({ queryKey: ['stocks', 'product', variables.productId] });
      void queryClient.invalidateQueries({ queryKey: ['products'] });
    },
  });
}
