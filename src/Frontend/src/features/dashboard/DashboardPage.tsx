import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowUpRight,
  Buildings,
  ChartLine,
  Package,
  Scales,
  Stack,
  Warehouse,
  WarningCircle,
  type Icon,
} from '@phosphor-icons/react';
import { QueryState } from '../../shared/components/QueryState';
import { Sparkline } from '../../shared/components/Sparkline';
import { formatPoids } from '../../shared/utils/formatPoids';
import { useLowStockProducts } from '../products/useProducts';
import { useDashboardStats } from './useDashboardStats';
import { Variation } from './Variation';
import type { DashboardStats } from './types';

/*
 * Chaque indicateur porte sa propre teinte : un tableau de bord entièrement gris
 * oblige à lire les libellés pour se repérer, alors que la couleur situe la tuile
 * du premier coup d'oeil. Les paires icône/pastille restent au-dessus de 4,5:1.
 */
const TONS = {
  marque: { pastille: 'bg-brand-900/10', icone: 'text-brand-900', valeur: 'text-slate-900' },
} as const;

type Ton = keyof typeof TONS;

/** Tuile d'indicateur : pastille colorée, chiffre dominant, lien vers l'écran détaillé. */
function Kpi({
  label,
  valeur,
  /** Rendu deja formate, utilise a la place de `valeur` quand il est fourni. */
  texte,
  legende,
  icone: Icone,
  ton = 'marque',
  enErreur,
  to,
  className = '',
}: {
  label: string;
  valeur?: number;
  texte?: string;
  /** Ligne de contexte sous le chiffre, ex. « 1 inactif ». */
  legende?: string;
  icone: Icon;
  ton?: Ton;
  /* Sans ce drapeau, une requete en echec laissait le squelette tourner
     indefiniment : l indicateur semblait charger alors que rien n arrivait plus. */
  enErreur?: boolean;
  to: string;
  className?: string;
}) {
  const t = TONS[ton];

  return (
    <Link to={to} className={`dashboard-card group flex flex-col justify-center ${className}`}>
      <div className="flex items-start justify-between gap-3">
        <span className={`flex h-10 w-10 items-center justify-center rounded-field ${t.pastille}`}>
          <Icone size={22} weight="duotone" className={t.icone} aria-hidden="true" />
        </span>
        {/* Rien n'indiquait que ces tuiles menaient quelque part. */}
        <ArrowUpRight
          size={18}
          className="text-slate-600 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5 group-hover:text-slate-800"
          aria-hidden="true"
        />
      </div>

      <p className="mt-4 text-sm font-medium text-slate-600">{label}</p>

      {texte !== undefined ? (
        <p className={`mt-1 text-3xl font-bold ${t.valeur}`}>{texte}</p>
      ) : valeur === undefined && enErreur ? (
        <p className="mt-1 text-3xl font-bold text-slate-600">
          <span aria-hidden="true">—</span>
          <span className="sr-only">Donnée indisponible</span>
        </p>
      ) : valeur === undefined ? (
        <span className="mt-2 block h-9 w-20 animate-pulse rounded bg-slate-200" aria-hidden="true" />
      ) : (
        <p className={`mt-1 text-3xl font-bold ${t.valeur}`}>{valeur.toLocaleString('fr-CA')}</p>
      )}

      {legende && (valeur !== undefined || texte !== undefined) && (
        <p className="mt-1 text-xs text-slate-600">{legende}</p>
      )}
    </Link>
  );
}

/**
 * Barre stock / seuil : un badge « 7 restants » obligeait à aller chercher le seuil
 * ailleurs pour juger de l'urgence. Le remplissage la donne d'un coup d'oeil.
 */
function BarreStock({ quantite, seuil }: { quantite: number; seuil: number }) {
  const part = seuil > 0 ? Math.min(1, quantite / seuil) : 1;
  const pourcent = Math.round(part * 100);
  /* amber-700 et non amber-500 : 4,0:1 contre la piste grise, au lieu de 1,7:1.
     La valeur chiffree reste affichee a cote, la barre n est pas le seul vecteur. */
  const ton = quantite === 0 ? 'bg-rose-700' : part < 0.34 ? 'bg-rose-600' : 'bg-amber-700';

  return (
    <div className="mt-2 flex items-center gap-3">
      <div
        className="h-2 flex-1 overflow-hidden rounded-full bg-slate-200"
        role="progressbar"
        aria-valuenow={quantite}
        aria-valuemin={0}
        aria-valuemax={seuil}
        aria-label={`${quantite} en stock sur un seuil de ${seuil}`}
      >
        <div
          className={`h-full rounded-full ${ton} transition-[width] duration-500 ease-out`}
          style={{ width: `${Math.max(pourcent, 3)}%` }}
        />
      </div>
      <span className="shrink-0 font-mono text-sm">
        <span className="font-semibold text-slate-900">{quantite}</span>
        <span className="text-slate-600"> / {seuil}</span>
      </span>
    </div>
  );
}

/**
 * Charge de chaque entrepôt.
 *
 * Trois entrepôts détiennent des volumes très inégaux : le chiffre global du stock
 * ne disait pas où il se trouve.
 */
function RepartitionEntrepots({ stats }: { stats: DashboardStats | undefined }) {
  const lignes = stats?.warehouseLoads;
  if (lignes && lignes.length === 0) return null;

  const max = lignes && lignes.length > 0 ? Math.max(...lignes.map((l) => l.totalQuantity)) : 1;
  const total = lignes?.reduce((t, l) => t + l.totalQuantity, 0) ?? 0;

  return (
    <section className="dashboard-card sm:col-span-2">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 items-center justify-center rounded-field bg-brand-900/10">
          <Warehouse size={22} weight="duotone" className="text-brand-900" aria-hidden="true" />
        </span>
        <h2 className="text-xl font-bold text-slate-800">Stock par entrepôt</h2>
      </div>

      {!lignes ? (
        <div className="mt-5 space-y-3" aria-hidden="true">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-10 animate-pulse rounded bg-slate-100" />
          ))}
        </div>
      ) : (
        <ul className="mt-5 space-y-4">
          {lignes.map((l) => (
            <li key={l.warehouseId}>
              <div className="flex items-baseline justify-between gap-3 text-sm">
                <span className="truncate font-medium text-slate-800" title={l.name}>
                  {l.name}
                </span>
                <span className="shrink-0 font-mono text-slate-700">
                  <span className="font-semibold text-slate-900">
                    {l.totalQuantity.toLocaleString('fr-CA')}
                  </span>
                  <span className="text-slate-600"> unités</span>
                </span>
              </div>
              <div className="mt-1 h-2 overflow-hidden rounded-full bg-slate-200">
                <div
                  className="h-full rounded-full bg-brand-700"
                  style={{ width: `${Math.max((l.totalQuantity / max) * 100, 2)}%` }}
                />
              </div>
              <p className="mt-1 text-xs text-slate-600">
                {l.productCount.toLocaleString('fr-CA')} référence{l.productCount > 1 ? 's' : ''}
                {total > 0 && ` · ${Math.round((l.totalQuantity / total) * 100)} % du stock`}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** Barres horizontales : combien de références par collection. */
function RepartitionCollections({ stats }: { stats: DashboardStats | undefined }) {
  /* Le regroupement se fait desormais en SQL : le client recevait auparavant tout
     le catalogue page par page uniquement pour compter ces lignes. */
  const lignes = stats?.topCollections.map((c) => ({ nom: c.collection, total: c.count }));

  if (lignes && lignes.length === 0) return null;

  const max = lignes && lignes.length > 0 ? Math.max(...lignes.map((l) => l.total)) : 1;

  return (
    <section className="dashboard-card sm:col-span-2">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 items-center justify-center rounded-field bg-brand-900/10">
          <Stack size={22} weight="duotone" className="text-brand-900" aria-hidden="true" />
        </span>
        <h2 className="text-xl font-bold text-slate-800">Références par collection</h2>
      </div>

      {!lignes ? (
        <div className="mt-5 space-y-3" aria-hidden="true">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-6 animate-pulse rounded bg-slate-100" />
          ))}
        </div>
      ) : (
        <ul className="mt-5 space-y-3">
          {lignes.map(({ nom, total }) => (
            <li key={nom} className="group flex items-center gap-3 text-sm">
              <span className="w-32 shrink-0 truncate text-slate-700 sm:w-44" title={nom}>
                {nom}
              </span>
              <div className="h-5 flex-1 overflow-hidden rounded bg-slate-100">
                {/* brand-700 : 4,7:1 contre la piste, contre 2,9:1 pour brand-500. */}
                <div
                  className="h-full rounded bg-brand-700 transition-[filter] duration-200 group-hover:brightness-125"
                  style={{ width: `${Math.max((total / max) * 100, 4)}%` }}
                />
              </div>
              <span className="w-8 shrink-0 text-right font-mono font-medium text-slate-800">
                {total}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** Courbe du flux net quotidien, avec sa variation par rapport à la période précédente. */
function FluxDeStock({ stats }: { stats: DashboardStats | undefined }) {
  const valeurs = stats?.dailyFlow.map((f) => f.net) ?? [];
  const entrees = stats?.dailyFlow.reduce((s, f) => s + f.in, 0) ?? 0;
  const sorties = stats?.dailyFlow.reduce((s, f) => s + f.out, 0) ?? 0;

  return (
    <section className="dashboard-card sm:col-span-2 lg:col-span-4">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-field bg-brand-900/10">
            <ChartLine size={22} weight="duotone" className="text-brand-900" aria-hidden="true" />
          </span>
          <div>
            <h2 className="text-xl font-bold text-slate-800">Flux de stock</h2>
            {/* Nommer ce qui est trace : « flux » seul laisserait croire a un niveau
                de stock, alors que la courbe montre un solde quotidien. */}
            <p className="text-sm text-slate-600">Entrées moins sorties, par jour</p>
          </div>
        </div>
        {stats && <Variation pourcentage={stats.changePercent} periodeJours={stats.periodDays} />}
      </div>

      {!stats ? (
        <div className="mt-5 h-16 animate-pulse rounded bg-slate-100" aria-hidden="true" />
      ) : (
        <>
          <Sparkline
            className="mt-5 h-16 w-full text-slate-500"
            valeurs={valeurs}
            hauteur={40}
            resume={`Solde quotidien sur ${stats.periodDays} jours : ${entrees} unités entrées, ${sorties} sorties, solde net de ${stats.netCurrentPeriod}.`}
          />
          <dl className="mt-4 flex flex-wrap gap-x-8 gap-y-2 text-sm">
            <div className="flex items-baseline gap-2">
              <dt className="text-slate-600">Entrées</dt>
              <dd className="font-mono font-semibold text-slate-900">{entrees.toLocaleString('fr-CA')}</dd>
            </div>
            <div className="flex items-baseline gap-2">
              <dt className="text-slate-600">Sorties</dt>
              <dd className="font-mono font-semibold text-slate-900">{sorties.toLocaleString('fr-CA')}</dd>
            </div>
            <div className="flex items-baseline gap-2">
              <dt className="text-slate-600">Solde</dt>
              <dd className="font-mono font-semibold text-slate-900">
                {stats.netCurrentPeriod > 0 && '+'}
                {stats.netCurrentPeriod.toLocaleString('fr-CA')}
              </dd>
            </div>
          </dl>
        </>
      )}
    </section>
  );
}

/**
 * Écran d'accueil. Il n'affichait qu'une seule liste — les produits sous le seuil —
 * sans aucun repère chiffré.
 *
 * Les agrégats viennent de `GET /dashboard/stats`, calculés en base. Ils étaient
 * auparavant dérivés côté client du catalogue complet, ce qui imposait de paginer
 * toutes les références à chaque visite pour obtenir deux totaux.
 */
/** Nombre de produits critiques repris sur l'accueil. */
const APERCU_STOCK_BAS = 6;

export function DashboardPage() {
  const { data, isLoading, isError, error } = useLowStockProducts(1, APERCU_STOCK_BAS);

  const { data: stats, isError: erreurStats } = useDashboardStats();

  const inactifs = stats?.inactiveWarehouses ?? 0;

  /*
   * Tri par criticité (quantité rapportée au seuil) plutôt qu'alphabétique : une
   * rupture totale doit passer devant un produit à 90 % de son seuil.
   * Le tri porte sur la page rendue, la liste restant paginée côté serveur.
   */
  const items = useMemo(() => {
    if (!data) return undefined;
    return [...data.items].sort((a, b) => {
      const ra = a.lowStockThreshold > 0 ? a.quantity / a.lowStockThreshold : 1;
      const rb = b.lowStockThreshold > 0 ? b.quantity / b.lowStockThreshold : 1;
      return ra - rb;
    });
  }, [data]);

  return (
    <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
      <h1 className="mb-6 text-2xl font-semibold tracking-tight text-slate-900">
        Tableau de bord
      </h1>

      {/*
        Grille bento : la liste de stock bas occupe deux colonnes sur deux rangées,
        les indicateurs se logent autour. Chaque cellule porte du contenu — aucune
        case vide à combler.
      */}
      <div className="grid grid-cols-1 items-start gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <section className="dashboard-card sm:col-span-2 lg:row-span-2">
          <div className="mb-5 flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
            <div className="flex items-center gap-3">
              <span className="flex h-10 w-10 items-center justify-center rounded-field bg-rose-100">
                <WarningCircle size={22} weight="duotone" className="text-rose-800" aria-hidden="true" />
              </span>
              <h2 className="text-xl font-bold text-slate-800">Produits en stock bas</h2>
            </div>
            {data && data.totalCount > 0 && (
              <span className="self-start rounded-full border border-rose-200 bg-rose-50 px-3 py-1 text-sm font-semibold text-rose-800 sm:self-auto">
                {data.totalCount} au total
              </span>
            )}
          </div>

          <QueryState
            isLoading={isLoading}
            isError={isError}
            error={error}
            isEmpty={items?.length === 0}
            emptyMessage="Aucun produit sous le seuil."
            className="mt-4"
            skeletonRows={4}
          />

          {items && items.length > 0 && (
            <>
              <ul className="divide-y divide-slate-100">
                {items.map((p) => (
                  <li key={p.id} className="dashboard-list-item">
                    <Link to={`/products/${p.id}`} className="group block px-2 py-3 sm:px-3">
                      <span
                        className="block truncate font-semibold text-slate-800 transition-colors group-hover:text-brand-900"
                        title={p.name}
                      >
                        {p.name}
                      </span>
                      <BarreStock quantite={p.quantity} seuil={p.lowStockThreshold} />
                    </Link>
                  </li>
                ))}
              </ul>
              {data!.totalCount > items.length && (
                <div className="mt-4 border-t border-slate-100 pt-4">
                  <Link
                    to="/products?stockBas=1"
                    className="btn-secondary w-full"
                  >
                    Voir les {data!.totalCount.toLocaleString('fr-CA')} produits sous le seuil
                  </Link>
                </div>
              )}
            </>
          )}
        </section>

        <Kpi
          label="Produits référencés"
          valeur={stats?.totalProducts}
          icone={Package}
          ton="marque"
          enErreur={erreurStats}
          to="/products"
        />
        <Kpi
          label="Entrepôts actifs"
          valeur={stats?.activeWarehouses}
          legende={inactifs > 0 ? `${inactifs} inactif${inactifs > 1 ? 's' : ''}` : undefined}
          icone={Buildings}
          ton="marque"
          enErreur={erreurStats}
          to="/warehouses"
        />
        <Kpi
          label="Poids total du stock"
          texte={stats ? formatPoids(stats.totalWeightGrams) : undefined}
          icone={Scales}
          ton="marque"
          enErreur={erreurStats}
          to="/products"
          className="sm:col-span-2"
        />

        <FluxDeStock stats={stats} />
        <RepartitionEntrepots stats={stats} />
        <RepartitionCollections stats={stats} />
      </div>
    </main>
  );
}
