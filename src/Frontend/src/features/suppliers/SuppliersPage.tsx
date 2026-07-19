import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { RequireRole } from '../../routes/RequireRole';
import { DataList } from '../../shared/components/DataList';
import { Field } from '../../shared/components/Field';
import { QueryState } from '../../shared/components/QueryState';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { createSupplierSchema, type CreateSupplierInput } from './types';
import { useCreateSupplier, useSuppliers } from './useSuppliers';

export function SuppliersPage() {
  const { data: suppliers, isLoading, isError, error } = useSuppliers();
  const createSupplier = useCreateSupplier();

  const form = useForm<CreateSupplierInput>({ resolver: zodResolver(createSupplierSchema) });

  async function onSubmit(input: CreateSupplierInput) {
    await createSupplier.mutateAsync(input);
    form.reset();
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-10">
      <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Fournisseurs</h1>

      <RequireRole role="Gestionnaire">
        <form
          className="mt-6 flex flex-wrap items-end gap-3 rounded-xl border border-slate-200 bg-white p-4"
          onSubmit={form.handleSubmit(onSubmit)}
        >
          <Field label="Nom" error={form.formState.errors.name?.message}>
            <input className="input w-56" {...form.register('name')} />
          </Field>
          <Field label="Email (optionnel)" error={form.formState.errors.contactEmail?.message}>
            <input className="input w-56" {...form.register('contactEmail')} />
          </Field>
          <Field label="Téléphone (optionnel)">
            <input className="input w-40" {...form.register('phone')} />
          </Field>
          <button type="submit" className="btn-primary" disabled={form.formState.isSubmitting}>
            Créer
          </button>
          {createSupplier.isError && (
            <p className="w-full rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600">
              {getErrorMessage(createSupplier.error, 'Impossible de créer le fournisseur. Réessaie.')}
            </p>
          )}
        </form>
      </RequireRole>

      <QueryState
        isLoading={isLoading}
        isError={isError}
        error={error}
        isEmpty={suppliers?.length === 0}
        emptyMessage="Aucun fournisseur pour l'instant."
      />

      {suppliers && suppliers.length > 0 && (
        <DataList
          items={suppliers}
          keyOf={(s) => s.id}
          renderItem={(s) => (
            <>
              <p className="font-medium text-slate-800">{s.name}</p>
              <p className="text-sm text-slate-500">
                {[s.contactEmail, s.phone].filter(Boolean).join(' · ') || '—'}
              </p>
            </>
          )}
        />
      )}
    </main>
  );
}
