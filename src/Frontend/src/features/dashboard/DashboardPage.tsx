import { useState } from 'react';
import { PaginationControls } from '../../shared/components/PaginationControls';
import { QueryState } from '../../shared/components/QueryState';
import { useLowStockProducts } from '../products/useProducts';

/**
 * Écran dashboard (squelette). Les KPIs et graphiques (voir plan §7)
 * seront branchés sur les endpoints d'agrégation une fois l'API complétée.
 */
export function DashboardPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, error } = useLowStockProducts(page);

  return (
    <main className="mx-auto max-w-5xl px-4 py-8 md:px-6 md:py-12">
      <h1 className="text-3xl md:text-4xl font-extrabold tracking-tight gradient-text mb-2">
        Tableau de bord
      </h1>

      <section className="mt-8">
        <div className="dashboard-card">
          <div className="flex flex-col sm:flex-row sm:items-baseline justify-between gap-2 mb-6">
            <h2 className="text-xl font-bold text-slate-800">Produits en stock bas</h2>
            {data && data.totalCount > 0 && (
              <span className="text-sm font-medium bg-rose-50 text-rose-600 px-3 py-1 rounded-full border border-rose-100 self-start sm:self-auto">
                {data.totalCount} au total
              </span>
            )}
          </div>

          <QueryState
            isLoading={isLoading}
            isError={isError}
            error={error}
            isEmpty={data?.items.length === 0}
            emptyMessage="Aucun produit sous le seuil."
            className="mt-4"
          />

          {data && data.items.length > 0 && (
            <>
              <ul className="divide-y divide-slate-100/50">
                {data.items.map((p) => (
                  <li key={p.id} className="dashboard-list-item flex flex-col sm:flex-row sm:items-center justify-between px-2 sm:px-4 py-4 gap-2">
                    <span className="font-semibold text-slate-700 truncate min-w-0" title={p.name}>{p.name}</span>
                    <span className="font-mono text-sm font-medium bg-rose-50 text-rose-600 px-3 py-1 rounded-lg border border-rose-100/50 whitespace-nowrap self-start sm:self-auto shrink-0">
                      {p.quantity} restants
                    </span>
                  </li>
                ))}
              </ul>
              <div className="mt-6 border-t border-slate-100 pt-6">
                <PaginationControls
                  page={data.page}
                  pageSize={data.pageSize}
                  totalCount={data.totalCount}
                  onPageChange={setPage}
                />
              </div>
            </>
          )}
        </div>
      </section>
    </main>
  );
}
