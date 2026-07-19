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
