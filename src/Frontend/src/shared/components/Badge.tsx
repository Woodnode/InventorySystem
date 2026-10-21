import type { ReactNode } from 'react';

/*
 * Les tons portent un sens, pas une couleur : « alerte » pour un stock sous le
 * seuil ou une sortie, « marque » pour une entrée, « inactif » pour une fiche
 * désactivée. Les classes vivent dans index.css, ce qui leur donne une variante
 * sombre ; les pastilles recopiées dans chaque écran n'en avaient pas.
 */
export type TonBadge = 'neutre' | 'marque' | 'alerte' | 'avertissement' | 'inactif';

const CLASSES_TON: Record<TonBadge, string> = {
  neutre: 'badge-neutre',
  marque: 'badge-marque',
  alerte: 'badge-alerte',
  avertissement: 'badge-avertissement',
  inactif: 'badge-inactif',
};

/** Pastille de statut ou de quantité, identique sur tous les écrans. */
export function Badge({
  ton = 'neutre',
  taille = 'sm',
  className = '',
  children,
}: {
  ton?: TonBadge;
  /** `md` pour l'en-tête d'une fiche, `sm` dans les listes. */
  taille?: 'sm' | 'md';
  className?: string;
  children: ReactNode;
}) {
  return (
    <span className={`badge ${taille === 'md' ? 'badge-md' : ''} ${CLASSES_TON[ton]} ${className}`}>
      {children}
    </span>
  );
}
