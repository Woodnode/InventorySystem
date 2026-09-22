import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../shared/api-client/client';
import { supplierSchema, type CreateSupplierInput, type Supplier } from './types';

const QUERY_KEY = ['suppliers'];

export function useSuppliers() {
  return useQuery<Supplier[]>({
    queryKey: QUERY_KEY,
    queryFn: async () => {
      const { data } = await apiClient.get('/suppliers');
      return supplierSchema.array().parse(data);
    },
  });
}

export function useCreateSupplier() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (input: CreateSupplierInput) => {
      const { data } = await apiClient.post<{ id: string }>('/suppliers', input);
      return data;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}

/** Modifie un fournisseur existant (voir AUDIT.md F-4 : le catalogue était create-only). */
export function useUpdateSupplier() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, ...input }: CreateSupplierInput & { id: string }) => {
      await apiClient.put(`/suppliers/${id}`, input);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}

/**
 * Active/désactive un fournisseur — remplace la suppression : un fournisseur référencé par
 * des produits existants ne doit jamais disparaître (parité avec useSetWarehouseActive,
 * manquante ici jusqu'au ré-audit).
 */
export function useSetSupplierActive() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, isActive }: { id: string; isActive: boolean }) => {
      await apiClient.patch(`/suppliers/${id}/active`, { isActive });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}
