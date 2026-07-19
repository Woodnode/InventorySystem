import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { RequireRole } from '../../routes/RequireRole';
import { DataList } from '../../shared/components/DataList';
import { Field } from '../../shared/components/Field';
import { QueryState } from '../../shared/components/QueryState';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { createWarehouseSchema, type CreateWarehouseInput } from './types';
import { useCreateWarehouse, useWarehouses } from './useWarehouses';

export function WarehousesPage() {
  const { data: warehouses, isLoading, isError, error } = useWarehouses();
  const createWarehouse = useCreateWarehouse();

  const form = useForm<CreateWarehouseInput>({ resolver: zodResolver(createWarehouseSchema) });

  async function onSubmit(input: CreateWarehouseInput) {
    await createWarehouse.mutateAsync(input);
    form.reset();
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-10">
      <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Entrepôts</h1>

      <RequireRole role="Gestionnaire">
        <form
          className="mt-6 flex flex-wrap items-end gap-3 rounded-xl border border-slate-200 bg-white p-4"
          onSubmit={form.handleSubmit(onSubmit)}
        >
          <Field label="Nom" error={form.formState.errors.name?.message}>
            <input className="input w-56" {...form.register('name')} />
          </Field>
          <Field label="Adresse (optionnel)">
            <input className="input w-72" {...form.register('address')} />
          </Field>
          <button type="submit" className="btn-primary" disabled={form.formState.isSubmitting}>
            Créer
          </button>
          {createWarehouse.isError && (
            <p className="w-full rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600">
              {getErrorMessage(createWarehouse.error, "Impossible de créer l'entrepôt. Réessaie.")}
            </p>
          )}
        </form>
      </RequireRole>

      <QueryState
        isLoading={isLoading}
        isError={isError}
        error={error}
        isEmpty={warehouses?.length === 0}
        emptyMessage="Aucun entrepôt pour l'instant."
      />

      {warehouses && warehouses.length > 0 && (
        <DataList
          items={warehouses}
          keyOf={(w) => w.id}
          renderItem={(w) => (
            <>
              <div>
                <p className="font-medium text-slate-800">{w.name}</p>
                {w.address && <p className="text-sm text-slate-500">{w.address}</p>}
              </div>
              {!w.isActive && (
                <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500">
                  Inactif
                </span>
              )}
            </>
          )}
        />
      )}
    </main>
  );
}
