import type { ReactNode } from 'react';

/**
 * Liste à puces standard (Products/Warehouses/Suppliers/Movements) : même conteneur
 * `divide-y` partout, seul le contenu de chaque ligne change. Le rendu loading/erreur/vide
 * qui précède reste géré par QueryState — ce composant ne s'occupe que de la liste
 * elle-même, une fois qu'on sait qu'il y a des éléments à afficher.
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
    <ul className={`${className} divide-y divide-slate-100 rounded-xl border border-slate-100 bg-white`}>
      {items.map((item) => (
        <li
          key={keyOf(item)}
          className={`flex items-center justify-between px-4 py-3 ${itemClassName}`}
        >
          {renderItem(item)}
        </li>
      ))}
    </ul>
  );
}
