import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import QRCode from 'react-qr-code';
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
      <Link 
        to="/products" 
        className="inline-flex items-center gap-2 px-4 py-2 bg-white hover:bg-slate-50 text-slate-700 text-sm font-medium rounded-xl border border-slate-200 shadow-sm transition-colors w-fit"
      >
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
        </svg>
        Retour aux produits
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
          {product.quantity} en stock · min. {product.lowStockThreshold}
        </span>
      </div>

      <div className="mt-6 grid grid-cols-1 md:grid-cols-2 gap-6">
        <section className="dashboard-card">
          <div className="flex justify-between items-start mb-4 border-b pb-2">
            <h2 className="text-lg font-medium text-slate-800">Informations Générales</h2>
            <div className="flex flex-col items-center bg-white p-2 border border-slate-200 rounded-md shadow-sm" title="Scannez avec l'app mobile">
              <div id="qr-print-section" className="flex flex-col items-center p-2 bg-white">
                <QRCode value={product.sku} size={64} level="M" />
                <span className="text-[10px] text-slate-700 mt-1 font-mono font-bold tracking-wider">{product.sku}</span>
              </div>
              <button 
                onClick={() => window.print()}
                className="mt-1 text-[10px] text-slate-500 hover:text-slate-800 underline uppercase tracking-wide font-medium"
              >
                Imprimer
              </button>
            </div>
          </div>
          <dl className="grid grid-cols-2 gap-x-4 gap-y-4 text-sm">
            <div>
              <dt className="text-slate-500 font-medium">SKU</dt>
              <dd className="text-slate-900 mt-1">{product.sku}</dd>
            </div>
            <div>
              <dt className="text-slate-500 font-medium">Type</dt>
              <dd className="text-slate-900 mt-1">{product.productType || '—'}</dd>
            </div>
            <div>
              <dt className="text-slate-500 font-medium">Collection</dt>
              <dd className="text-slate-900 mt-1">{product.collection || '—'}</dd>
            </div>
            <div>
              <dt className="text-slate-500 font-medium">Vol., N°</dt>
              <dd className="text-slate-900 mt-1">{product.volumeNumber || '—'}</dd>
            </div>
            <div>
              <dt className="text-slate-500 font-medium">Compagnie</dt>
              <dd className="text-slate-900 mt-1">{product.company || '—'}</dd>
            </div>
            <div>
              <dt className="text-slate-500 font-medium">Code Projet</dt>
              <dd className="text-slate-900 mt-1">{product.projectCode || '—'}</dd>
            </div>
            <div>
              <dt className="text-slate-500 font-medium">Année</dt>
              <dd className="text-slate-900 mt-1">{product.year || '—'}</dd>
            </div>
            <div>
              <dt className="text-slate-500 font-medium">Poids unitaire (lb)</dt>
              <dd className="text-slate-900 mt-1">{product.weightPerCopyLb ? `${product.weightPerCopyLb} lb` : '—'}</dd>
            </div>
            <div>
              <dt className="text-slate-500 font-medium">Poids total estimé</dt>
              <dd className="text-slate-900 mt-1 font-semibold">{product.weightPerCopyLb ? `${(product.weightPerCopyLb * product.quantity).toFixed(2)} lb` : '—'}</dd>
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
                    <span className="font-mono bg-blue-100 text-blue-800 px-2 py-0.5 rounded text-xs">{s.quantity} unités</span>
                  </div>
                  <div className="grid grid-cols-2 gap-2 text-xs text-slate-600">
                    <div><span className="text-slate-400">Section:</span> {s.section || '—'}</div>
                    <div><span className="text-slate-400">Espace:</span> {s.space || '—'}</div>
                    <div><span className="text-slate-400">Palette CAF:</span> {s.pallet || '—'}</div>
                    <div><span className="text-slate-400">Distributeur:</span> {s.distributorName || '—'}</div>
                    <div><span className="text-slate-400">Boîtes:</span> {s.boxesCount || 0}</div>
                    <div><span className="text-slate-400">Par boîte:</span> {s.copiesPerBox || 0} copies</div>
                    <div>
                      <span className="text-slate-400">Dernière prise d'inventaire:</span>{' '}
                      {s.inventoryDate ? new Date(s.inventoryDate).toLocaleDateString('fr-CA') : '—'}
                    </div>
                    <div><span className="text-slate-400">Responsable:</span> {s.responsibleName || '—'}</div>
                  </div>
                  {s.comment && (
                    <div className="mt-2 text-xs text-slate-500 bg-white p-2 rounded border border-slate-100">
                      📝 {s.comment}
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
