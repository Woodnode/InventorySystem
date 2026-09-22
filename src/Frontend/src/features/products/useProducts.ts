import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../shared/api-client/client';
import { pagedResultSchema, type PagedResult } from '../../shared/api-client/pagination';
import { downloadBlob, extractFilename } from '../../shared/utils/downloadBlob';
import { productSchema, type CreateProductInput, type Product } from './types';

const QUERY_KEY = ['products'];
const PAGE_SIZE = 20;

/** Doit rester aligné avec InventorySystem.Application.Products.Dtos.ProductSortBy (backend). */
export type ProductSortBy = 'Name' | 'Sku' | 'Quantity';

/** Filtres de recherche/liste produits — voir ProductsController.GetAll (backend). */
export type ProductFilters = {
  search?: string;
  minQuantity?: number;
  maxQuantity?: number;
  lowStockOnly?: boolean;
  sortBy?: ProductSortBy;
  sortDescending?: boolean;
};

/**
 * Hook TanStack Query : une page de produits, avec stock total agrégé (voir plan §7).
 * Le catalogue grandit sans borne — voir Backend Application/Common/Dtos/PagedResult.
 * `pageSize` par défaut convient à l'écran de liste (ProductsPage) ; les sélecteurs qui
 * ont besoin de la totalité (ex. le menu déroulant produit de MovementsPage) demandent
 * une grande page plutôt qu'un vrai contrôle de pagination — voir MovementsPage.tsx.
 * `filters` (recherche SKU/nom, quantité min/max, stock bas, tri) est inclus dans la clé de
 * requête pour que chaque combinaison ait son propre cache et se rafraîchisse au changement.
 */
export function useProducts(page = 1, pageSize = PAGE_SIZE, filters: ProductFilters = {}) {
  const { search, minQuantity, maxQuantity, lowStockOnly, sortBy, sortDescending } = filters;
  return useQuery<PagedResult<Product>>({
    queryKey: [
      ...QUERY_KEY, page, pageSize, search ?? '', minQuantity ?? null, maxQuantity ?? null,
      lowStockOnly ?? false, sortBy ?? 'Name', sortDescending ?? false,
    ],
    queryFn: async () => {
      const { data } = await apiClient.get('/products', {
        params: {
          page,
          pageSize,
          search: search || undefined,
          minQuantity: minQuantity ?? undefined,
          maxQuantity: maxQuantity ?? undefined,
          lowStockOnly: lowStockOnly || undefined,
          sortBy: sortBy || undefined,
          sortDescending: sortDescending || undefined,
        },
      });
      return pagedResultSchema(productSchema).parse(data);
    },
  });
}

/** Toutes les pages de produits pour un sélecteur (plafond de sécurité 50 pages × 100). */
export function useAllProductsForPicker() {
  return useQuery<Product[]>({
    queryKey: [...QUERY_KEY, 'picker-all'],
    queryFn: async () => {
      const pageSize = 100;
      const maxPages = 50;
      const items: Product[] = [];
      let page = 1;
      let totalCount = Number.POSITIVE_INFINITY;

      while (page <= maxPages && items.length < totalCount) {
        const { data } = await apiClient.get('/products', { params: { page, pageSize } });
        const parsed = pagedResultSchema(productSchema).parse(data);
        totalCount = parsed.totalCount;
        items.push(...parsed.items);
        if (parsed.items.length === 0) break;
        page++;
      }

      return items;
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

const LOW_STOCK_PAGE_SIZE = 20;

/**
 * Produits en rupture / sous leur seuil de réappro — utilisé par le dashboard.
 * Paginé côté serveur comme useProducts (voir AUDIT.md F-3) : un catalogue avec plus de
 * 20 produits sous le seuil ne doit pas être tronqué silencieusement.
 */
export function useLowStockProducts(page = 1, pageSize = LOW_STOCK_PAGE_SIZE) {
  return useQuery<PagedResult<Product>>({
    queryKey: [...QUERY_KEY, 'low-stock', page, pageSize],
    queryFn: async () => {
      const { data } = await apiClient.get('/products/low-stock', { params: { page, pageSize } });
      return pagedResultSchema(productSchema).parse(data);
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

export function useImportProducts() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData();
      formData.append('file', file);
      const { data } = await apiClient.post<{ productsCreated: number, productsUpdated: number, errors: string[] }>('/products/import', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      });
      return data;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
      void queryClient.invalidateQueries({ queryKey: ['stocks'] });
    },
  });
}
