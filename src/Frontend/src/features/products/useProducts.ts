import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../shared/api-client/client';
import { pagedResultSchema, type PagedResult } from '../../shared/api-client/pagination';
import { downloadBlob, extractFilename } from '../../shared/utils/downloadBlob';
import { productSchema, type CreateProductInput, type Product } from './types';

const QUERY_KEY = ['products'];
const PAGE_SIZE = 20;

/**
 * Hook TanStack Query : une page de produits, avec stock total agrégé (voir plan §7).
 * Le catalogue grandit sans borne — voir Backend Application/Common/Dtos/PagedResult.
 * `pageSize` par défaut convient à l'écran de liste (ProductsPage) ; les sélecteurs qui
 * ont besoin de la totalité (ex. le menu déroulant produit de MovementsPage) demandent
 * une grande page plutôt qu'un vrai contrôle de pagination — voir MovementsPage.tsx.
 */
export function useProducts(page = 1, pageSize = PAGE_SIZE) {
  return useQuery<PagedResult<Product>>({
    queryKey: [...QUERY_KEY, page, pageSize],
    queryFn: async () => {
      const { data } = await apiClient.get('/products', { params: { page, pageSize } });
      return pagedResultSchema(productSchema).parse(data);
    },
  });
}

/**
 * Un seul produit par id, avec son stock total agrégé — nécessaire pour ProductDetailPage
 * depuis que GET /products est paginé (un produit hors de la page courante du cache liste
 * serait sinon introuvable à tort).
 */
export function useProduct(id: string | undefined) {
  return useQuery<Product>({
    queryKey: [...QUERY_KEY, id],
    queryFn: async () => {
      const { data } = await apiClient.get(`/products/${id}`);
      return productSchema.parse(data);
    },
    enabled: !!id,
  });
}

/** Produits en rupture / sous leur seuil de réappro — utilisé par le dashboard. */
export function useLowStockProducts() {
  return useQuery<Product[]>({
    queryKey: [...QUERY_KEY, 'low-stock'],
    queryFn: async () => {
      const { data } = await apiClient.get('/products/low-stock');
      return productSchema.array().parse(data);
    },
  });
}

/** Télécharge le catalogue produits en CSV ou Excel (voir plan §13, export différé). */
export async function exportProducts(format: 'Csv' | 'Excel'): Promise<void> {
  const response = await apiClient.get('/products/export', {
    params: { format },
    responseType: 'blob',
  });
  const filename = extractFilename(
    response.headers['content-disposition'] as string | undefined,
    format === 'Csv' ? 'produits.csv' : 'produits.xlsx',
  );
  downloadBlob(response.data as Blob, filename);
}

export function useCreateProduct() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (input: CreateProductInput) => {
      const payload = {
        ...input,
        description: input.description || null,
        supplierId: input.supplierId || null,
      };
      const { data } = await apiClient.post<{ id: string }>('/products', payload);
      return data;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
      void queryClient.invalidateQueries({ queryKey: ['stocks'] });
    },
  });
}
