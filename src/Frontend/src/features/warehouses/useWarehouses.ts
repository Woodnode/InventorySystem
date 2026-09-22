import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../shared/api-client/client';
import { warehouseSchema, type CreateWarehouseInput, type Warehouse } from './types';

const QUERY_KEY = ['warehouses'];

/** Liste des entrepôts (voir plan §7 : TanStack Query pour tout appel API). */
export function useWarehouses() {
  return useQuery<Warehouse[]>({
    queryKey: QUERY_KEY,
    queryFn: async () => {
      const { data } = await apiClient.get('/warehouses');
      return warehouseSchema.array().parse(data);
    },
  });
}

export function useCreateWarehouse() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (input: CreateWarehouseInput) => {
      const { data } = await apiClient.post<{ id: string }>('/warehouses', input);
      return data;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}

/** Modifie nom/adresse (voir AUDIT.md F-4 : le catalogue était create-only). */
export function useUpdateWarehouse() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, ...input }: CreateWarehouseInput & { id: string }) => {
      await apiClient.put(`/warehouses/${id}`, input);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}

/**
 * Active/désactive un entrepôt — remplace la suppression : un entrepôt référencé par du
 * stock ou des mouvements historiques ne doit jamais disparaître (voir AUDIT.md F-4).
 */
export function useSetWarehouseActive() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, isActive }: { id: string; isActive: boolean }) => {
      await apiClient.patch(`/warehouses/${id}/active`, { isActive });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}
