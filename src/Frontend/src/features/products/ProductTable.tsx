import { Link } from 'react-router-dom';
import { CaretDown, CaretUp, CaretUpDown } from '@phosphor-icons/react';
import type { Product } from './types';
import type { ProductSortBy } from './useProducts';

/** Colonnes triables par l'API : les autres ne portent pas de bouton. */
const TRIABLES: Partial<Record<string, ProductSortBy>> = {
  nom: 'Name',
  sku: 'Sku',
  quantite: 'Quantity',
};

function EnTete({
  cle,
  libelle,
  sortBy,
  sortDescending,
  onSort,
  className = '',
}: {
  cle: string;
  libelle: string;
  sortBy: ProductSortBy;
  sortDescending: boolean;
  onSort: (colonne: ProductSortBy) => void;
  className?: string;
}) {
  const colonne = TRIABLES[cle];

  if (!colonne) {
    return (
      <th scope="col" className={`px-3 py-2 text-left font-semibold text-slate-700 ${className}`}>
        {libelle}
      </th>
    );
  }

  const actif = sortBy === colonne;
  const Icone = actif ? (sortDescending ? CaretDown : CaretUp) : CaretUpDown;

  return (
    <th
      scope="col"
      /* aria-sort porte l'etat du tri pour les lecteurs d'ecran ; l'icone seule
         ne le dirait qu'aux personnes qui la voient. */
      aria-sort={actif ? (sortDescending ? 'descending' : 'ascending') : 'none'}
      className={`px-3 py-2 text-left font-semibold text-slate-700 ${className}`}
    >
      <button
        type="button"
        onClick={() => onSort(colonne)}
        className="-mx-2 flex min-h-11 items-center gap-1 rounded-field px-2 hover:bg-slate-100"
      >
        {libelle}
        <Icone size={14} weight="bold" className={actif ? 'text-brand-900' : 'text-slate-600'} aria-hidden="true" />
      </button>
    </th>
  );
}

/**
 * Catalogue en tableau, pour les écrans qui ont la largeur.
 *
 * La liste verticale demandait une ligne par produit et cachait l'emplacement
 * derrière un clic : comparer deux références imposait de faire défiler. Le tableau
 * met SKU, collection, emplacement et quantité côte à côte, et confie le tri à
 * l'API, qui sait déjà le faire sur le catalogue entier plutôt que sur la page.
 *
 * Sous 1024px, c'est la liste qui reste affichée : cinq colonnes ne tiennent pas
 * sur un téléphone.
 */
export function ProductTable({
  items,
  sortBy,
  sortDescending,
  onSort,
}: {
  items: Product[];
  sortBy: ProductSortBy;
  sortDescending: boolean;
  onSort: (colonne: ProductSortBy) => void;
}) {
  const entete = { sortBy, sortDescending, onSort };

  return (
    <div className="surface mt-6 hidden overflow-hidden lg:block">
      <table className="w-full border-collapse text-sm">
        <caption className="sr-only">
          Catalogue des produits. Les en-têtes Nom, SKU et Quantité permettent de trier.
        </caption>
        <thead className="border-b border-slate-200 bg-slate-50">
          <tr>
            <EnTete cle="nom" libelle="Produit" {...entete} />
            <EnTete cle="sku" libelle="SKU" {...entete} className="w-40" />
            <EnTete cle="collection" libelle="Collection" {...entete} className="w-48" />
            <EnTete cle="emplacement" libelle="Emplacement" {...entete} className="w-56" />
            <EnTete cle="quantite" libelle="Quantité" {...entete} className="w-32" />
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {items.map((p) => (
            <tr key={p.id} className="transition-colors hover:bg-slate-50">
              <td className="px-3 py-2">
                <Link
                  to={`/products/${p.id}`}
                  className="font-medium text-slate-800 hover:text-brand-900 hover:underline"
                >
                  {p.name}
                </Link>
                {p.productType && (
                  <span className="ml-2 text-xs text-slate-600">{p.productType}</span>
                )}
              </td>
              <td className="px-3 py-2 font-mono text-xs text-slate-700">{p.sku}</td>
              <td className="px-3 py-2 text-slate-700">{p.collection || '—'}</td>
              <td className="px-3 py-2 text-slate-700">{p.primaryLocation || '—'}</td>
              <td className="px-3 py-2">
                <span
                  className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-xs font-semibold ${
                    p.isLowOnStock
                      ? 'bg-rose-100 text-rose-800'
                      : 'bg-slate-100 text-slate-700'
                  }`}
                >
                  {p.quantity.toLocaleString('fr-CA')}
                  {p.isLowOnStock && <span>Stock bas</span>}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
