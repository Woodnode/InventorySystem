import { useQuery } from '@tanstack/react-query';
import { apiClient } from '../../shared/api-client/client';
import { dashboardStatsSchema, type DashboardStats } from './types';

/**
 * Agrégats du tableau de bord, calculés en base.
 *
 * Remplace le calcul côté client, qui paginait le catalogue entier (jusqu'à
 * cinquante requêtes) pour obtenir deux nombres et une répartition.
 */
export function useDashboardStats(periodDays = 30) {
  return useQuery<DashboardStats>({
    queryKey: ['dashboard', 'stats', periodDays],
    queryFn: async () => {
      const { data } = await apiClient.get('/dashboard/stats', { params: { periodDays } });
      return dashboardStatsSchema.parse(data);
    },
  });
}
