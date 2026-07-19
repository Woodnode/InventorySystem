import { QueryState } from '../../shared/components/QueryState';
import { useLowStockProducts } from '../products/useProducts';

/**
 * Écran dashboard (squelette). Les KPIs et graphiques Recharts (voir plan §7)
 * seront branchés sur les endpoints d'agrégation une fois l'API complétée.
 */
export function DashboardPage() {
  const { data, isLoading, isError, error } = useLowStockProducts();

  return (
    <main className="mx-auto max-w-5xl px-6 py-12">
      <h1 className="text-3xl font-semibold tracking-tight text-slate-900">
        Tableau de bord — Inventaire
      </h1>
      <p className="mt-2 text-slate-500">
        Système d'inventaire Web + Mobile · API ASP.NET Core (Clean Architecture)
      </p>

      <section className="mt-10">
        <h2 className="text-lg font-medium text-slate-800">Produits en stock bas</h2>

        <QueryState
          isLoading={isLoading}
          isError={isError}
          error={error}
          isEmpty={data?.length === 0}
          emptyMessage="Aucun produit sous le seuil."
          className="mt-4"
        />

        {data && data.length > 0 && (
          <ul className="mt-4 divide-y divide-slate-100 rounded-xl border border-slate-100">
            {data.map((p) => (
              <li key={p.id} className="flex items-center justify-between px-4 py-3">
                <span className="font-medium text-slate-800">{p.name}</span>
                <span className="font-mono text-sm text-rose-600">
                  {p.quantity} / seuil {p.lowStockThreshold}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
