import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ExportButtons } from '../../shared/components/ExportButtons';
import { PaginationControls } from '../../shared/components/PaginationControls';
import { useAllProductsForPicker } from '../products/useProducts';
import { useWarehouses } from '../warehouses/useWarehouses';
import { exportMovements, useMovementsByProduct } from './useMovements';
import { useRecentMovements } from './useRecentMovements';
import { formatMovementType } from '../../shared/utils/movementLabels';
import { QueryState } from '../../shared/components/QueryState';
import { MovementForm } from './MovementForm';
import { MovementList } from './MovementList';

/**
 * Point d'entrée général des mouvements : sélection d'un produit, puis formulaire
 * + historique (mêmes hooks/composants que ProductDetailPage — voir plan §7).
 */
export function MovementsPage() {
  const { data: products, isLoading } = useAllProductsForPicker();
  const { data: warehouses } = useWarehouses();
  const [productId, setProductId] = useState('');
  /* Le catalogue depasse les six cents references : derouler une liste native
     aussi longue pour retrouver un titre est impraticable. */
  const [recherche, setRecherche] = useState('');
  const [movementsPage, setMovementsPage] = useState(1);
  const { data: recents, isLoading: chargeRecents, isError: erreurRecents } = useRecentMovements();

  const {
    data: movementsPageData,
    isLoading: isLoadingMovements,
    isError: isErrorMovements,
    error: errorMovements,
  } = useMovementsByProduct(productId || undefined, movementsPage);

  const filtres = useMemo(() => {
    if (!products) return undefined;
    const terme = recherche.trim().toLowerCase();
    if (!terme) return products;
    return products.filter((p) => `${p.name} ${p.sku}`.toLowerCase().includes(terme));
  }, [products, recherche]);

  return (
    <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Mouvements</h1>
        <ExportButtons onExport={(format) => exportMovements(format)} />
      </div>
      <p className="mt-2 text-xs text-slate-600">
        L'export ci-dessus couvre l'historique complet, tous produits confondus.
      </p>

      <div className="mt-6 flex flex-col gap-3 text-sm sm:max-w-md">
        <div className="flex flex-col gap-1">
          <label htmlFor="mouvement-recherche" className="font-medium text-slate-700">
            Rechercher un produit
          </label>
          <input
            id="mouvement-recherche"
            type="search"
            className="input"
            placeholder="SKU ou titre"
            value={recherche}
            onChange={(e) => setRecherche(e.target.value)}
            disabled={isLoading}
          />
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="mouvement-produit" className="font-medium text-slate-700">
            Produit
            {filtres && (
              <span className="ml-2 font-normal text-slate-600">
                {filtres.length.toLocaleString('fr-CA')} résultat{filtres.length > 1 ? 's' : ''}
              </span>
            )}
          </label>
          <select
            id="mouvement-produit"
            className="input"
            value={productId}
            onChange={(e) => {
              setProductId(e.target.value);
              setMovementsPage(1);
            }}
            disabled={isLoading}
            /* La liste reste deroulante plutot que de devenir une combobox :
               le controle natif garde la recherche clavier et l'accessibilite
               que reimplementer couterait cher pour un gain mince. */
            size={filtres && filtres.length > 1 && recherche ? Math.min(filtres.length + 1, 8) : undefined}
          >
            <option value="">— choisir un produit —</option>
            {filtres?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name} · {p.sku}
              </option>
            ))}
          </select>
          {filtres?.length === 0 && (
            <p className="text-slate-600">Aucun produit ne correspond à « {recherche} ».</p>
          )}
        </div>
        {productId && (
          <Link
            to={`/products/${productId}`}
            className="mt-2 flex min-h-11 w-fit items-center text-sm text-slate-700 underline underline-offset-2"
          >
            Voir la fiche produit
          </Link>
        )}
      </div>

      {!productId && (
        <section className="mt-8">
          <h2 className="text-lg font-medium text-slate-800">Derniers mouvements</h2>
          <p className="mt-1 text-sm text-slate-600">
            Tous produits confondus. Choisis un produit ci-dessus pour voir son historique
            complet et enregistrer un mouvement.
          </p>

          <QueryState
            isLoading={chargeRecents}
            isError={erreurRecents}
            isEmpty={recents?.length === 0}
            emptyMessage="Aucun mouvement enregistré pour l'instant."
            className="mt-3"
            skeletonRows={4}
          />

          {recents && recents.length > 0 && (
            <ul className="surface mt-3 divide-y divide-slate-100">
              {recents.map((m) => (
                <li
                  key={m.id}
                  className="flex flex-col gap-2 px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between sm:gap-4"
                >
                  <div className="min-w-0">
                    <Link
                      to={`/products/${m.productId}`}
                      className="font-medium text-slate-800 hover:text-brand-900 hover:underline"
                    >
                      {m.productName}
                    </Link>
                    <span className="ml-2 font-mono text-xs text-slate-600">{m.productSku}</span>
                    <p className="mt-1 text-xs text-slate-600">
                      {formatMovementType(m.type)} · {m.warehouseName}
                      {m.toWarehouseName && ` → ${m.toWarehouseName}`}
                      {m.reason && ` · ${m.reason}`}
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-3">
                    {/* Le signe dit le sens du mouvement sans qu'il faille lire le libelle. */}
                    <span
                      className={`rounded-full px-2 py-0.5 font-mono text-xs font-semibold ${
                        m.type === 'In'
                          ? 'bg-brand-900/10 text-brand-900'
                          : m.type === 'Out'
                            ? 'bg-rose-100 text-rose-800'
                            : 'bg-slate-100 text-slate-700'
                      }`}
                    >
                      {m.type === 'In' ? '+' : m.type === 'Out' ? '−' : ''}
                      {m.quantity.toLocaleString('fr-CA')}
                    </span>
                    <time dateTime={m.createdAtUtc} className="text-xs text-slate-600">
                      {new Date(m.createdAtUtc).toLocaleString('fr-CA')}
                    </time>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      {productId && (
        <>
          <section className="mt-6">
            <MovementForm productId={productId} />
          </section>

          <section className="mt-8">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <h2 className="text-lg font-medium text-slate-800">Historique</h2>
              <ExportButtons onExport={(format) => exportMovements(format, productId)} />
            </div>
            <QueryState
              isLoading={isLoadingMovements}
              isError={isErrorMovements}
              error={errorMovements}
              isEmpty={movementsPageData?.items.length === 0}
              emptyMessage="Aucun mouvement pour l'instant."
              className="mt-3"
            />
            {movementsPageData && movementsPageData.items.length > 0 && (
              <>
                <MovementList
                  movements={movementsPageData.items}
                  warehouses={warehouses}
                  className="mt-3"
                />
                <PaginationControls
                  page={movementsPageData.page}
                  pageSize={movementsPageData.pageSize}
                  totalCount={movementsPageData.totalCount}
                  onPageChange={setMovementsPage}
                />
              </>
            )}
          </section>
        </>
      )}
    </main>
  );
}
