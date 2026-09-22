import { useState, useRef, useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { RequireRole } from '../../routes/RequireRole';
import { DataList } from '../../shared/components/DataList';
import { ExportButtons } from '../../shared/components/ExportButtons';
import { Field } from '../../shared/components/Field';
import { PaginationControls } from '../../shared/components/PaginationControls';
import { QueryState } from '../../shared/components/QueryState';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
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

  // Recherche texte (SKU ou nom) : débouncée pour éviter une requête par frappe.
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [minQuantity, setMinQuantity] = useState('');
  const [maxQuantity, setMaxQuantity] = useState('');
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [sortBy, setSortBy] = useState<ProductSortBy>('Name');
  const [sortDescending, setSortDescending] = useState(false);

  useEffect(() => {
    const timeout = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(timeout);
  }, [searchInput]);

  // Tout changement de filtre ou de tri invalide la page courante (la page 3 d'une
  // recherche précédente n'a aucun sens pour de nouveaux critères).
  useEffect(() => {
    setPage(1);
  }, [search, minQuantity, maxQuantity, lowStockOnly, sortBy, sortDescending]);

  const hasActiveFilters = search !== '' || minQuantity !== '' || maxQuantity !== '' || lowStockOnly;

  function resetFilters() {
    setSearchInput('');
    setSearch('');
    setMinQuantity('');
    setMaxQuantity('');
    setLowStockOnly(false);
  }

  const { data, isLoading, isError, error } = useProducts(page, undefined, {
    search: search || undefined,
    minQuantity: minQuantity === '' ? undefined : Number(minQuantity),
    maxQuantity: maxQuantity === '' ? undefined : Number(maxQuantity),
    lowStockOnly,
    sortBy,
    sortDescending,
  });
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
    } catch {
      // L'erreur est déjà exposée via createProduct.isError/error (bannière ci-dessous) ;
      // le catch ici évite juste un rejet de promesse non géré (voir ré-audit).
    }
  }

  const handleImport = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      try {
        const result = await importProducts.mutateAsync(file);
        alert(`Import terminé !\nProduits créés: ${result.productsCreated}\nProduits existants mis à jour: ${result.productsUpdated}\nErreurs: ${result.errors.length}`);
      } catch (err) {
        alert("Erreur lors de l'import: " + getErrorMessage(err, "Échec de l'import."));
      }
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  return (
    <main className="mx-auto max-w-5xl px-6 py-10">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Produits</h1>
        <div className="flex items-center gap-2">
          <input type="file" accept=".csv,.xlsx,.xls,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/vnd.ms-excel" className="hidden" ref={fileInputRef} onChange={handleImport} />
          <button 
            className="btn-secondary flex gap-2 items-center" 
            onClick={() => fileInputRef.current?.click()}
            disabled={importProducts.isPending}
          >
            {importProducts.isPending ? (
               <span className="w-4 h-4 rounded-full border-2 border-slate-400 border-t-slate-700 animate-spin"></span>
            ) : (
               <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" /></svg>
            )}
            Importer CSV / Excel
          </button>
          <ExportButtons onExport={exportProducts} />
        </div>
      </div>

      <RequireRole role="Gestionnaire">
        {activeWarehouses && activeWarehouses.length === 0 ? (
          <p className="mt-6 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-700">
            Crée (ou réactive) d'abord un entrepôt avant de pouvoir ajouter un produit.
          </p>
        ) : (
          <form
            className="mt-6 grid grid-cols-2 gap-3 rounded-xl border border-slate-200 bg-white p-4 sm:grid-cols-3"
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
              <p className="col-span-full rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600">
                {getErrorMessage(createProduct.error, 'Impossible de créer le produit. Réessaie.')}
              </p>
            )}
            <div className="col-span-full">
              <button type="submit" className="btn-primary" disabled={form.formState.isSubmitting}>
                Créer le produit
              </button>
            </div>
          </form>
        )}
      </RequireRole>

      <div className="mt-6 flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4 sm:flex-row sm:flex-wrap sm:items-end">
        <div className="flex flex-1 flex-col gap-1 text-sm sm:min-w-[220px]">
          <label htmlFor="product-search" className="font-medium text-slate-700">
            Recherche (SKU ou nom)
          </label>
          <input
            id="product-search"
            type="text"
            className="input"
            placeholder="Ex. 00181 ou Défi avant les fêtes"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1 text-sm">
          <label htmlFor="product-min-qty" className="font-medium text-slate-700">
            Quantité min.
          </label>
          <input
            id="product-min-qty"
            type="number"
            min={0}
            className="input w-28"
            value={minQuantity}
            onChange={(e) => setMinQuantity(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1 text-sm">
          <label htmlFor="product-max-qty" className="font-medium text-slate-700">
            Quantité max.
          </label>
          <input
            id="product-max-qty"
            type="number"
            min={0}
            className="input w-28"
            value={maxQuantity}
            onChange={(e) => setMaxQuantity(e.target.value)}
          />
        </div>
        <label className="flex items-center gap-2 text-sm font-medium text-slate-700 pb-2 sm:pb-2.5">
          <input
            type="checkbox"
            className="h-4 w-4 rounded border-slate-300"
            checked={lowStockOnly}
            onChange={(e) => setLowStockOnly(e.target.checked)}
          />
          Stock bas uniquement
        </label>
        <div className="flex flex-col gap-1 text-sm">
          <label htmlFor="product-sort-by" className="font-medium text-slate-700">
            Trier par
          </label>
          <div className="flex gap-1">
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
            <button
              type="button"
              className="btn-secondary px-3"
              title={sortDescending ? 'Décroissant' : 'Croissant'}
              onClick={() => setSortDescending((d) => !d)}
            >
              {sortDescending ? '↓' : '↑'}
            </button>
          </div>
        </div>
        {hasActiveFilters && (
          <button type="button" className="btn-secondary text-sm" onClick={resetFilters}>
            Réinitialiser
          </button>
        )}
      </div>

      <QueryState
        isLoading={isLoading}
        isError={isError}
        error={error}
        isEmpty={data?.items.length === 0}
        emptyMessage={hasActiveFilters ? 'Aucun produit ne correspond à ces critères.' : "Aucun produit pour l'instant."}
      />

      {data && data.items.length > 0 && (
        <>
          <DataList
            items={data.items}
            keyOf={(p) => p.id}
            renderItem={(p) => (
              <>
                <div className="flex flex-col">
                  <Link to={`/products/${p.id}`} className="font-medium text-slate-800 hover:underline">
                    {p.name} <span className="text-slate-400">· {p.sku}</span>
                  </Link>
                  <div className="flex gap-2 mt-1 text-xs text-slate-500">
                    {p.collection && <span className="bg-slate-100 px-2 py-0.5 rounded">{p.collection}</span>}
                    {p.productType && <span className="bg-slate-100 px-2 py-0.5 rounded">{p.productType}</span>}
                    {p.year && <span className="bg-slate-100 px-2 py-0.5 rounded">{p.year}</span>}
                  </div>
                </div>
                <span className={`font-mono text-sm ${p.isLowOnStock ? 'text-rose-600' : 'text-slate-600'}`}>
                  {p.quantity} restants
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
        </>
      )}
    </main>
  );
}
