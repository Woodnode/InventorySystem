import { useState, useRef, useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Barcode, UploadSimple } from '@phosphor-icons/react';
import { RequireRole } from '../../routes/RequireRole';
import { CreatePanel } from '../../shared/components/CreatePanel';
import { DataList } from '../../shared/components/DataList';
import { ExportButtons } from '../../shared/components/ExportButtons';
import { Field } from '../../shared/components/Field';
import { PaginationControls } from '../../shared/components/PaginationControls';
import { QueryState } from '../../shared/components/QueryState';
import { ProductTable } from './ProductTable';
import { ScannerCodeBarres } from './ScannerCodeBarres';
import { apiClient } from '../../shared/api-client/client';
import { productSchema } from './types';
import { useCollections } from './useCollections';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { useToast } from '../../shared/hooks/useToast';
import { useWarehouses } from '../warehouses/useWarehouses';
import { useSuppliers } from '../suppliers/useSuppliers';
import { createProductSchema, type CreateProductInput } from './types';
import { exportProducts, useCreateProduct, useProducts, useImportProducts, type ProductSortBy } from './useProducts';

const sortOptions: { value: ProductSortBy; label: string }[] = [
  { value: 'Name', label: 'Nom' },
  { value: 'Sku', label: 'SKU' },
  { value: 'Quantity', label: 'Quantité' },
];

// Valeurs par défaut du formulaire — utilisées à la fois à l'initialisation et après
// une création réussie. Doit lister TOUS les champs : `form.reset(values)` ne réinitialise
// que les clés présentes dans `values`, les autres conservent leur dernière saisie.
const emptyProductForm: CreateProductInput = {
  sku: '',
  name: '',
  description: '',
  lowStockThreshold: 0,
  warehouseId: '',
  initialQuantity: 0,
  supplierId: '',
  boxesCount: 0,
  copiesPerBox: 0,
};

export function ProductsPage() {
  const [page, setPage] = useState(1);
  const { notify } = useToast();
  const navigate = useNavigate();
  const [scanOuvert, setScanOuvert] = useState(false);

  // Recherche texte (SKU ou nom) : débouncée pour éviter une requête par frappe.
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [minQuantity, setMinQuantity] = useState('');
  const [maxQuantity, setMaxQuantity] = useState('');
  /* `?stockBas=1` permet au tableau de bord de renvoyer directement vers la
     liste filtree, plutot que de demander a l'utilisateur de refaire le filtre. */
  const [parametres, setParametres] = useSearchParams();
  const [lowStockOnly, setLowStockOnly] = useState(parametres.get('stockBas') === '1');
  const [collection, setCollection] = useState('');
  const [sortBy, setSortBy] = useState<ProductSortBy>('Name');
  const [sortDescending, setSortDescending] = useState(false);
  // Deplies, les six controles de filtre occupaient a eux seuls un ecran de telephone.
  const [filtresOuverts, setFiltresOuverts] = useState(false);

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  // Tout changement de filtre ou de tri invalide la page courante (la page 3 d'une
  // recherche précédente n'a aucun sens pour de nouveaux critères).
  useEffect(() => {
    setPage(1);
  }, [search, minQuantity, maxQuantity, lowStockOnly, collection, sortBy, sortDescending]);

  const hasActiveFilters =
    search !== '' || minQuantity !== '' || maxQuantity !== '' || lowStockOnly || collection !== '';

  useEffect(() => {
    const present = parametres.get('stockBas') === '1';
    if (present === lowStockOnly) return;
    const suivant = new URLSearchParams(parametres);
    if (lowStockOnly) suivant.set('stockBas', '1');
    else suivant.delete('stockBas');
    setParametres(suivant, { replace: true });
  }, [lowStockOnly, parametres, setParametres]);

  function resetFilters() {
    setSearchInput('');
    setSearch('');
    setMinQuantity('');
    setMaxQuantity('');
    setLowStockOnly(false);
    setCollection('');
  }

  const { data, isLoading, isError, error } = useProducts(page, undefined, {
    search: search || undefined,
    minQuantity: minQuantity === '' ? undefined : Number(minQuantity),
    maxQuantity: maxQuantity === '' ? undefined : Number(maxQuantity),
    lowStockOnly,
    collection: collection || undefined,
    sortBy,
    sortDescending,
  });
  const { data: collections } = useCollections();
  const { data: warehouses } = useWarehouses();
  // Un entrepôt désactivé ne doit pas pouvoir recevoir un nouveau produit (voir ré-audit).
  const activeWarehouses = warehouses?.filter((w) => w.isActive);
  const { data: suppliers } = useSuppliers();
  const activeSuppliers = suppliers?.filter((s) => s.isActive);
  const createProduct = useCreateProduct();

  const importProducts = useImportProducts();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const form = useForm<CreateProductInput>({
    resolver: zodResolver(createProductSchema),
    defaultValues: emptyProductForm,
  });

  async function onSubmit(input: CreateProductInput) {
    try {
      await createProduct.mutateAsync(input);
      form.reset(emptyProductForm);
      // Le formulaire se vidait sans rien confirmer : impossible de distinguer une
      // création réussie d'une saisie perdue.
      notify(`Produit « ${input.name} » créé.`);
    } catch {
      // L'erreur est déjà exposée via createProduct.isError/error (bannière ci-dessous) ;
      // le catch ici évite juste un rejet de promesse non géré (voir ré-audit).
    }
  }

  /*
   * Un code lu vaut un SKU : les QR des fiches en portent un, et les codes-barres
   * des ouvrages correspondent a la reference du catalogue. On interroge l'API
   * plutot que de filtrer la page affichee, qui n'en montre que vingt.
   */
  const resoudreCode = async (code: string) => {
    try {
      const { data } = await apiClient.get(`/products/by-sku/${encodeURIComponent(code)}`);
      const produit = productSchema.parse(data);
      setScanOuvert(false);
      navigate(`/products/${produit.id}`);
      return true;
    } catch {
      // Code inconnu : on le signale et le scanner poursuit son balayage.
      notify(`Aucun produit ne porte le code « ${code} ».`, 'info');
      return false;
    }
  };

  const handleImport = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      try {
        const result = await importProducts.mutateAsync(file);
        const lignes = [
          `Import terminé.`,
          `${result.productsCreated} produit(s) créé(s), ${result.productsUpdated} mis à jour.`,
          result.errors.length > 0 ? `${result.errors.length} erreur(s).` : null,
        ].filter(Boolean);
        notify(lignes.join('\n'), result.errors.length > 0 ? 'info' : 'success');
      } catch (err) {
        notify("Erreur lors de l'import : " + getErrorMessage(err, "Échec de l'import."), 'error');
      }
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  return (
    <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Produits</h1>
        <div className="flex flex-wrap items-center gap-2">
          <input type="file" accept=".csv,.xlsx,.xls,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/vnd.ms-excel" className="hidden" ref={fileInputRef} onChange={handleImport} />
            <button
              className="btn-secondary"
              onClick={() => fileInputRef.current?.click()}
              disabled={importProducts.isPending}
            >
              {importProducts.isPending ? (
                <span className="h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-700" aria-hidden="true" />
              ) : (
                <UploadSimple size={16} aria-hidden="true" />
              )}
              Importer
            </button>
          <button type="button" className="btn-secondary" onClick={() => setScanOuvert(true)}>
            <Barcode size={18} aria-hidden="true" />
            Scanner
          </button>
          <ExportButtons onExport={exportProducts} />
        </div>
      </div>

      <RequireRole role="Gestionnaire">
        {activeWarehouses && activeWarehouses.length === 0 ? (
          <p className="mt-6 rounded-field bg-amber-50 px-3 py-2 text-sm text-amber-900">
            Crée (ou réactive) d'abord un entrepôt avant de pouvoir ajouter un produit.
          </p>
        ) : (
          <CreatePanel label="Nouveau produit">
            <form
              className="grid grid-cols-1 gap-3 surface p-4 sm:grid-cols-2 lg:grid-cols-3"
              onSubmit={form.handleSubmit(onSubmit)}
            >
              <Field label="SKU" error={form.formState.errors.sku?.message}>
                <input className="input" {...form.register('sku')} />
              </Field>
              <Field label="Nom" error={form.formState.errors.name?.message}>
                <input className="input" {...form.register('name')} />
              </Field>
              <Field label="Description (optionnel)">
                <input className="input" {...form.register('description')} />
              </Field>
              <Field label="Seuil de stock bas" error={form.formState.errors.lowStockThreshold?.message}>
                <input
                  type="number"
                  className="input"
                  {...form.register('lowStockThreshold', { valueAsNumber: true })}
                />
              </Field>
              <Field label="Entrepôt" error={form.formState.errors.warehouseId?.message}>
                <select className="input" {...form.register('warehouseId')}>
                  <option value="">— choisir —</option>
                  {activeWarehouses?.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.name}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Quantité initiale" error={form.formState.errors.initialQuantity?.message}>
                <input
                  type="number"
                  className="input"
                  {...form.register('initialQuantity', { valueAsNumber: true })}
                />
              </Field>
              <Field label="Fournisseur (optionnel)">
                <select className="input" {...form.register('supplierId')}>
                  <option value="">— aucun —</option>
                  {activeSuppliers?.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </Field>
              {createProduct.isError && (
                <p role="alert" className="col-span-full form-error">
                  {getErrorMessage(createProduct.error, 'Impossible de créer le produit. Réessaie.')}
                </p>
              )}
              <div className="col-span-full">
                <button type="submit" className="btn-primary w-full sm:w-auto" disabled={form.formState.isSubmitting}>
                  {form.formState.isSubmitting ? 'Création…' : 'Créer le produit'}
                </button>
              </div>
            </form>
          </CreatePanel>
        )}
      </RequireRole>

      <section className="mt-6">
        <h2 className="sr-only">Filtres</h2>
        <button
          type="button"
          className="btn-secondary w-full sm:hidden"
          aria-expanded={filtresOuverts}
          aria-controls="filtres-produits"
          onClick={() => setFiltresOuverts((o) => !o)}
        >
          <span aria-hidden="true">{filtresOuverts ? '−' : '+'}</span>
          Filtres{hasActiveFilters ? ' (actifs)' : ''}
        </button>
        {/* `hidden` retire les champs du parcours au clavier quand le panneau est
            ferme ; au-dela de 640px le panneau est toujours affiche. */}
        <div
          id="filtres-produits"
          className={`${filtresOuverts ? 'mt-3 flex' : 'hidden'} flex-col gap-3 surface p-4 sm:mt-0 sm:flex sm:flex-row sm:flex-wrap sm:items-end`}
        >
          <div className="flex flex-1 flex-col gap-1 text-sm sm:min-w-[220px]">
            <label htmlFor="product-search" className="font-medium text-slate-700">
              Recherche (SKU ou nom)
            </label>
            <input
              id="product-search"
              type="search"
              className="input"
              placeholder="Ex. 00181 ou Défi avant les fêtes"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
          </div>
          <div className="flex gap-3">
            <div className="flex flex-1 flex-col gap-1 text-sm">
              <label htmlFor="product-min-qty" className="font-medium text-slate-700">
                Quantité min.
              </label>
              <input
                id="product-min-qty"
                type="number"
                min={0}
                className="input sm:w-28"
                value={minQuantity}
                onChange={(e) => setMinQuantity(e.target.value)}
              />
            </div>
            <div className="flex flex-1 flex-col gap-1 text-sm">
              <label htmlFor="product-max-qty" className="font-medium text-slate-700">
                Quantité max.
              </label>
              <input
                id="product-max-qty"
                type="number"
                min={0}
                className="input sm:w-28"
                value={maxQuantity}
                onChange={(e) => setMaxQuantity(e.target.value)}
              />
            </div>
          </div>
          {collections && collections.length > 0 && (
            <div className="flex flex-col gap-1 text-sm">
              <label htmlFor="product-collection" className="font-medium text-slate-700">
                Collection
              </label>
              <select
                id="product-collection"
                className="input sm:max-w-[13rem]"
                value={collection}
                onChange={(e) => setCollection(e.target.value)}
              >
                <option value="">Toutes</option>
                {collections.map((c) => (
                  <option key={c.name} value={c.name}>
                    {c.name} ({c.count})
                  </option>
                ))}
              </select>
            </div>
          )}

          <label className="flex min-h-11 items-center gap-2 text-sm font-medium text-slate-700">
            <input
              type="checkbox"
              className="h-5 w-5 rounded border-slate-400"
              checked={lowStockOnly}
              onChange={(e) => setLowStockOnly(e.target.checked)}
            />
            Stock bas uniquement
          </label>
          <div className="flex flex-col gap-1 text-sm">
            <label htmlFor="product-sort-by" className="font-medium text-slate-700">
              Trier par
            </label>
            <div className="flex gap-2">
              <select
                id="product-sort-by"
                className="input"
                value={sortBy}
                onChange={(e) => setSortBy(e.target.value as ProductSortBy)}
              >
                {sortOptions.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
              {/* `title` seul n'est pas restitué de façon fiable : le sens du tri
                  n'était annoncé nulle part au clavier. */}
              <button
                type="button"
                className="btn-secondary min-w-11 px-3"
                aria-label={sortDescending ? 'Tri décroissant, activer le tri croissant' : 'Tri croissant, activer le tri décroissant'}
                onClick={() => setSortDescending((d) => !d)}
              >
                <span aria-hidden="true">{sortDescending ? '↓' : '↑'}</span>
              </button>
            </div>
          </div>
          {hasActiveFilters && (
            <button type="button" className="btn-secondary" onClick={resetFilters}>
              Réinitialiser
            </button>
          )}
        </div>
      </section>

      {/* Le total remonté par l'API n'était affiché nulle part. */}
      {data && data.totalCount > 0 && (
        <p aria-live="polite" className="mt-4 text-sm text-slate-600">
          {data.totalCount} produit{data.totalCount > 1 ? 's' : ''}
          {hasActiveFilters ? ' correspondant aux filtres' : ''}
        </p>
      )}

      <QueryState
        isLoading={isLoading}
        isError={isError}
        error={error}
        isEmpty={data?.items.length === 0}
        emptyMessage={hasActiveFilters ? 'Aucun produit ne correspond à ces critères.' : "Aucun produit pour l'instant."}
        emptyAction={
          hasActiveFilters ? (
            <button type="button" className="btn-secondary" onClick={resetFilters}>
              Réinitialiser les filtres
            </button>
          ) : undefined
        }
      />

      {data && data.items.length > 0 && (
        <div
          key={`${search}|${minQuantity}|${maxQuantity}|${lowStockOnly}|${sortBy}|${sortDescending}|${page}`}
          className="liste-entre"
        >
          <ProductTable
            items={data.items}
            sortBy={sortBy}
            sortDescending={sortDescending}
            /* Recliquer sur la colonne active inverse le sens, comme partout ailleurs. */
            onSort={(colonne) => {
              if (colonne === sortBy) setSortDescending((d) => !d);
              else {
                setSortBy(colonne);
                setSortDescending(false);
              }
            }}
          />

          <DataList
            className="mt-6 lg:hidden"
            items={data.items}
            keyOf={(p) => p.id}
            renderItem={(p) => (
              <>
                <div className="flex min-w-0 flex-col">
                  <Link to={`/products/${p.id}`} className="font-medium text-slate-800 hover:underline">
                    {/* text-slate-600 : le SKU est une donnée, pas une décoration —
                        text-slate-400 le laissait à 2,6:1. */}
                    {p.name} <span className="text-slate-600">· {p.sku}</span>
                  </Link>
                  {(p.collection || p.productType || p.year) && (
                    <div className="mt-1 flex flex-wrap gap-2 text-xs text-slate-700">
                      {p.collection && <span className="rounded bg-slate-100 px-2 py-0.5">{p.collection}</span>}
                      {p.productType && <span className="rounded bg-slate-100 px-2 py-0.5">{p.productType}</span>}
                      {p.year && <span className="rounded bg-slate-100 px-2 py-0.5">{p.year}</span>}
                    </div>
                  )}
                  {/* L'emplacement demandait d'ouvrir la fiche pour etre connu. */}
                  {p.primaryLocation && (
                    <p className="mt-1 text-xs text-slate-600">{p.primaryLocation}</p>
                  )}
                </div>
                <span className={`inline-flex shrink-0 items-center gap-2 font-mono text-sm ${p.isLowOnStock ? 'font-semibold text-rose-700' : 'text-slate-600'}`}>
                  {p.quantity} restants
                  {p.isLowOnStock && (
                    <span className="rounded-full bg-rose-100 px-2 py-0.5 text-xs font-semibold text-rose-800">
                      Stock bas
                    </span>
                  )}
                </span>
              </>
            )}
          />
          <PaginationControls
            page={data.page}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            onPageChange={setPage}
          />
        </div>
      )}
      {scanOuvert && (
        <ScannerCodeBarres onCode={resoudreCode} onClose={() => setScanOuvert(false)} />
      )}
    </main>
  );
}
