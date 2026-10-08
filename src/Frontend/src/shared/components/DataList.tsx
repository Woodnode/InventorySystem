import type { ReactNode } from 'react';

/**
 * Liste à puces standard (Products/Warehouses/Suppliers/Movements) : même conteneur
 * `divide-y` partout, seul le contenu de chaque ligne change. Le rendu loading/erreur/vide
 * qui précède reste géré par QueryState — ce composant ne s'occupe que de la liste
 * elle-même, une fois qu'on sait qu'il y a des éléments à afficher.
 *
 * La ligne passe en colonne sous 640px. Auparavant `flex items-center justify-between`
 * s'appliquait à toutes les largeurs : sur mobile, le libellé et sa valeur se
 * comprimaient l'un contre l'autre, et les lignes portant des boutons d'action
 * (entrepôts, fournisseurs) devenaient inutilisables.
 */
export function DataList<T>({
  items,
  keyOf,
  renderItem,
  className = 'mt-6',
  itemClassName = '',
}: {
  items: T[];
  keyOf: (item: T) => string;
  renderItem: (item: T) => ReactNode;
  className?: string;
  itemClassName?: string;
}) {
  return (
    <ul className={`${className} surface divide-y divide-slate-100`}>
      {items.map((item) => (
        <li
          key={keyOf(item)}
          className={`flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4 ${itemClassName}`}
        >
          {renderItem(item)}
        </li>
      ))}
    </ul>
  );
}
