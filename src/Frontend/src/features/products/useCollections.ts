import { useQuery } from '@tanstack/react-query';
import { apiClient } from '../../shared/api-client/client';
import { collectionSchema, type Collection } from './types';

/**
 * Collections du catalogue, avec leur nombre de références.
 *
 * Sert le filtre de l'écran Produits : retrouver une collection supposait jusqu'ici
 * d'en connaître le nom exact et de le saisir à la main.
 */
export function useCollections() {
  return useQuery<Collection[]>({
    queryKey: ['products', 'collections'],
    queryFn: async () => {
      const { data } = await apiClient.get('/products/collections');
      return collectionSchema.array().parse(data);
    },
    // La liste bouge rarement : inutile de la redemander à chaque visite.
    staleTime: 5 * 60_000,
  });
}
