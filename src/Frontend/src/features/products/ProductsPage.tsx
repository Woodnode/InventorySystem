import { useState } from 'react';
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
import { exportProducts, useCreateProduct, useProducts } from './useProducts';

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
};

export function ProductsPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, error } = useProducts(page);
  const { data: warehouses } = useWarehouses();
  const { data: suppliers } = useSuppliers();
  const createProduct = useCreateProduct();

  const form = useForm<CreateProductInput>({
    resolver: zodResolver(createProductSchema),
    defaultValues: emptyProductForm,
  });

  async function onSubmit(input: CreateProductInput) {
    await createProduct.mutateAsync(input);
    form.reset(emptyProductForm);
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-10">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Produits</h1>
        <ExportButtons onExport={exportProducts} />
      </div>

      <RequireRole role="Gestionnaire">
        {warehouses && warehouses.length === 0 ? (
          <p className="mt-6 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-700">
            Crée d'abord un entrepôt avant de pouvoir ajouter un produit.
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
                {warehouses?.map((w) => (
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
                {suppliers?.map((s) => (
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

      <QueryState
        isLoading={isLoading}
        isError={isError}
        error={error}
        isEmpty={data?.items.length === 0}
        emptyMessage="Aucun produit pour l'instant."
      />

      {data && data.items.length > 0 && (
        <>
          <DataList
            items={data.items}
            keyOf={(p) => p.id}
            renderItem={(p) => (
              <>
                <Link to={`/products/${p.id}`} className="font-medium text-slate-800 hover:underline">
                  {p.name} <span className="text-slate-400">· {p.sku}</span>
                </Link>
                <span className={`font-mono text-sm ${p.isLowOnStock ? 'text-rose-600' : 'text-slate-600'}`}>
                  {p.quantity} / seuil {p.lowStockThreshold}
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
