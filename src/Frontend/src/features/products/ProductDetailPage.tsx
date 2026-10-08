import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import QRCode from 'react-qr-code';
import { ExportButtons } from '../../shared/components/ExportButtons';
import { PaginationControls } from '../../shared/components/PaginationControls';
import { QueryState } from '../../shared/components/QueryState';
import { formatPoids } from '../../shared/utils/formatPoids';
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
      <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
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
      <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
        <p className="text-rose-700">Produit introuvable.</p>
        <Link to="/products" className="mt-2 inline-flex min-h-11 items-center text-sm text-slate-700 underline">
          Retour aux produits
        </Link>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
      <nav aria-label="Fil d'Ariane" className="text-sm">
        <ol className="flex min-w-0 items-center gap-2">
          <li className="shrink-0">
            <Link to="/products" className="font-medium text-brand-900 underline-offset-2 hover:underline">
              Produits
            </Link>
          </li>
          <li aria-hidden="true" className="text-slate-500">/</li>
          <li className="truncate text-slate-700">{product.name}</li>
        </ol>
      </nav>

      <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900">{product.name}</h1>
          <p className="text-sm text-slate-600">
            SKU {product.sku} {product.description && `· ${product.description}`}
          </p>
        </div>
        <span
          className={`w-fit shrink-0 rounded-full px-3 py-1 text-sm font-medium ${
            product.isLowOnStock ? 'bg-rose-100 text-rose-800' : 'bg-slate-100 text-slate-700'
          }`}
        >
          {product.isLowOnStock ? 'Stock bas · ' : ''}
          {product.quantity} en stock · min. {product.lowStockThreshold}
        </span>
      </div>

      <div className="mt-6 grid grid-cols-1 md:grid-cols-2 gap-6">
        <section className="dashboard-card">
          <div className="flex justify-between items-start mb-4 border-b pb-2">
            <h2 className="text-lg font-medium text-slate-800">Informations Générales</h2>
            <div className="flex flex-col items-center rounded-field border border-slate-200 bg-white p-2" title="Scannez avec l'app mobile">
              <div id="qr-print-section" className="flex flex-col items-center bg-white p-2">
                <QRCode value={product.sku} size={96} level="M" />
                <span className="mt-1 font-mono text-xs font-bold tracking-wider text-slate-700">{product.sku}</span>
              </div>
              <button
                type="button"
                onClick={() => window.print()}
                className="mt-1 flex min-h-11 items-center text-xs font-medium uppercase tracking-wide text-slate-700 underline hover:text-slate-900"
              >
                <span className="sr-only">Imprimer le code QR du produit </span>Imprimer
              </button>
            </div>
          </div>
          <dl className="grid grid-cols-1 gap-x-4 gap-y-4 text-sm sm:grid-cols-2">
            <div>
              <dt className="font-medium text-slate-600">SKU</dt>
              <dd className="text-slate-900 mt-1">{product.sku}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-600">Type</dt>
              <dd className="text-slate-900 mt-1">{product.productType || '—'}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-600">Collection</dt>
              <dd className="text-slate-900 mt-1">{product.collection || '—'}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-600">Vol., N°</dt>
              <dd className="text-slate-900 mt-1">{product.volumeNumber || '—'}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-600">Compagnie</dt>
              <dd className="text-slate-900 mt-1">{product.company || '—'}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-600">Code Projet</dt>
              <dd className="text-slate-900 mt-1">{product.projectCode || '—'}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-600">Année</dt>
              <dd className="text-slate-900 mt-1">{product.year || '—'}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-600">Poids unitaire</dt>
              <dd className="text-slate-900 mt-1">{formatPoids(product.weightPerCopyGrams)}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-600">Poids total estimé</dt>
              <dd className="text-slate-900 mt-1 font-semibold">
                {/* Un produit sans stock donne un total de zero, qui se lit comme une
                    anomalie ; le tiret dit la meme chose sans preter a confusion. */}
                {formatPoids(
                  product.weightPerCopyGrams && product.quantity > 0
                    ? product.weightPerCopyGrams * product.quantity
                    : null,
                )}
              </dd>
            </div>
          </dl>
        </section>

        <section className="dashboard-card">
          <h2 className="text-lg font-medium text-slate-800 mb-4 border-b pb-2">Logistique (par entrepôt)</h2>
          <QueryState
            isLoading={isLoadingStocks}
            isError={isErrorStocks}
            error={errorStocks}
            isEmpty={stocks?.length === 0}
            emptyMessage="Aucun stock enregistré."
          />
          {stocks && stocks.length > 0 && (
            <div className="space-y-4">
              {stocks.map(s => (
                <div key={s.warehouseId} className="bg-slate-50 p-3 rounded-lg border border-slate-100">
                  <div className="flex justify-between items-center mb-2">
                    <span className="font-semibold text-slate-800">{warehouseName(s.warehouseId)}</span>
                    <span className="rounded bg-brand-900/10 px-2 py-0.5 font-mono text-xs font-medium text-brand-900">{s.quantity} unités</span>
                  </div>
                  <div className="grid grid-cols-1 gap-2 text-xs text-slate-700 sm:grid-cols-2">
                    <div><span className="text-slate-600">Section:</span> {s.section || '—'}</div>
                    <div><span className="text-slate-600">Espace:</span> {s.space || '—'}</div>
                    <div><span className="text-slate-600">Palette CAF:</span> {s.pallet || '—'}</div>
                    <div><span className="text-slate-600">Distributeur:</span> {s.distributorName || '—'}</div>
                    <div><span className="text-slate-600">Boîtes:</span> {s.boxesCount || 0}</div>
                    <div><span className="text-slate-600">Par boîte:</span> {s.copiesPerBox || 0} copies</div>
                    <div>
                      <span className="text-slate-600">Dernière prise d'inventaire:</span>{' '}
                      {s.inventoryDate ? new Date(s.inventoryDate).toLocaleDateString('fr-CA') : '—'}
                    </div>
                    <div><span className="text-slate-600">Responsable:</span> {s.responsibleName || '—'}</div>
                  </div>
                  {s.comment && (
                    <div className="mt-2 rounded border border-slate-200 bg-white p-2 text-xs text-slate-700">
                      {s.comment}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </section>
      </div>

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
