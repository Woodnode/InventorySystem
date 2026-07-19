import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { DataList } from '../../shared/components/DataList';
import { ExportButtons } from '../../shared/components/ExportButtons';
import { PaginationControls } from '../../shared/components/PaginationControls';
import { QueryState } from '../../shared/components/QueryState';
import { useWarehouses } from '../warehouses/useWarehouses';
import { useProduct } from './useProducts';
import { useStockByProduct } from '../stocks/useStocks';
import { exportMovements, useMovementsByProduct } from '../movements/useMovements';
import { MovementForm } from '../movements/MovementForm';
import { MovementList } from '../movements/MovementList';
import { warehouseNameLookup } from '../../shared/utils/movementLabels';

/**
 * Fiche produit : répartition du stock par entrepôt, formulaire de mouvement et
 * historique. Le produit est lu via GET /products/{id} (pas depuis le cache liste
 * paginé — un produit hors de la page courante y serait introuvable à tort).
 */
export function ProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [movementsPage, setMovementsPage] = useState(1);

  const {
    data: product,
    isLoading: isLoadingProduct,
    isError: isErrorProduct,
    error: errorProduct,
  } = useProduct(id);
  const { data: warehouses } = useWarehouses();
  const {
    data: stocks,
    isLoading: isLoadingStocks,
    isError: isErrorStocks,
    error: errorStocks,
  } = useStockByProduct(id);
  const {
    data: movementsPageData,
    isLoading: isLoadingMovements,
    isError: isErrorMovements,
    error: errorMovements,
  } = useMovementsByProduct(id, movementsPage);

  const warehouseName = warehouseNameLookup(warehouses);

  if (isLoadingProduct || isErrorProduct) {
    return (
      <main className="mx-auto max-w-5xl px-6 py-10">
        <QueryState
          isLoading={isLoadingProduct}
          isError={isErrorProduct}
          error={errorProduct}
          className=""
        />
      </main>
    );
  }

  if (!product) {
    return (
      <main className="mx-auto max-w-5xl px-6 py-10">
        <p className="text-rose-600">Produit introuvable.</p>
        <Link to="/products" className="mt-2 inline-block text-sm text-slate-500 underline">
          Retour aux produits
        </Link>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-10">
      <Link to="/products" className="text-sm text-slate-500 underline underline-offset-2">
        ← Produits
      </Link>

      <div className="mt-2 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900">{product.name}</h1>
          <p className="text-sm text-slate-500">
            SKU {product.sku} {product.description && `· ${product.description}`}
          </p>
        </div>
        <span
          className={`rounded-full px-3 py-1 text-sm font-medium ${
            product.isLowOnStock ? 'bg-rose-50 text-rose-600' : 'bg-slate-100 text-slate-600'
          }`}
        >
          {product.quantity} en stock · seuil {product.lowStockThreshold}
        </span>
      </div>

      <section className="mt-8">
        <h2 className="text-lg font-medium text-slate-800">Répartition par entrepôt</h2>
        <QueryState
          isLoading={isLoadingStocks}
          isError={isErrorStocks}
          error={errorStocks}
          isEmpty={stocks?.length === 0}
          emptyMessage="Aucun stock enregistré."
          className="mt-3"
        />
        {stocks && stocks.length > 0 && (
          <DataList
            items={stocks}
            keyOf={(s) => s.warehouseId}
            className="mt-3"
            renderItem={(s) => (
              <>
                <span className="text-slate-700">{warehouseName(s.warehouseId)}</span>
                <span className="font-mono text-sm text-slate-600">{s.quantity}</span>
              </>
            )}
          />
        )}
      </section>

      <section className="mt-8">
        <h2 className="text-lg font-medium text-slate-800">Enregistrer un mouvement</h2>
        <div className="mt-3">
          <MovementForm productId={product.id} />
        </div>
      </section>

      <section className="mt-8">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-lg font-medium text-slate-800">Historique</h2>
          <ExportButtons onExport={(format) => exportMovements(format, product.id)} />
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
            <MovementList movements={movementsPageData.items} warehouses={warehouses} className="mt-3" />
            <PaginationControls
              page={movementsPageData.page}
              pageSize={movementsPageData.pageSize}
              totalCount={movementsPageData.totalCount}
              onPageChange={setMovementsPage}
            />
          </>
        )}
      </section>
    </main>
  );
}
