import { Badge } from '../../shared/components/Badge';
import type { MovementType } from './types';

/**
 * Quantité signée d'un mouvement : le signe dit le sens sans qu'il faille lire le
 * libellé. Un transfert ne change pas le stock total, il reste neutre et sans signe.
 */
export function BadgeMouvement({ type, quantite }: { type: MovementType; quantite: number }) {
  const ton = type === 'In' ? 'marque' : type === 'Out' ? 'alerte' : 'neutre';
  const signe = type === 'In' ? '+' : type === 'Out' ? '−' : '';

  return (
    <Badge ton={ton}>
      {signe}
      {quantite.toLocaleString('fr-CA')}
    </Badge>
  );
}
