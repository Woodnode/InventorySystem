import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ExportButtons } from '../../shared/components/ExportButtons';
import { PaginationControls } from '../../shared/components/PaginationControls';
import { QueryState } from '../../shared/components/QueryState';
import { useAllProductsForPicker } from '../products/useProducts';
import { useWarehouses } from '../warehouses/useWarehouses';
import { exportMovements, useMovementsByProduct } from './useMovements';
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
  const [movementsPage, setMovementsPage] = useState(1);

  const {
    data: movementsPageData,
    isLoading: isLoadingMovements,
    isError: isErrorMovements,
    error: errorMovements,
  } = useMovementsByProduct(productId || undefined, movementsPage);

  return (
    <main className="mx-auto max-w-5xl px-6 py-10">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Mouvements</h1>
        <ExportButtons onExport={(format) => exportMovements(format)} />
      </div>
      <p className="mt-1 text-xs text-slate-400">
        L'export ci-dessus couvre l'historique complet, tous produits confondus.
      </p>

      <div className="mt-6 flex flex-col gap-1 text-sm">
        <label className="font-medium text-slate-700">Produit</label>
        <select
          className="input max-w-sm"
          value={productId}
          onChange={(e) => {
            setProductId(e.target.value);
            setMovementsPage(1);
          }}
          disabled={isLoading}
        >
          <option value="">— choisir un produit —</option>
          {products?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name} · {p.sku}
            </option>
          ))}
        </select>
        {productId && (
          <Link
            to={`/products/${productId}`}
            className="mt-1 w-fit text-xs text-slate-500 underline underline-offset-2"
          >
            Voir la fiche produit
          </Link>
        )}
      </div>

      {productId && (
        <>
          <section className="mt-6">
            <MovementForm productId={productId} />
          </section>

          <section className="mt-8">
            <div className="flex items-center justify-between gap-4">
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
