import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { RequireRole } from '../../routes/RequireRole';
import { DataList } from '../../shared/components/DataList';
import { Field } from '../../shared/components/Field';
import { QueryState } from '../../shared/components/QueryState';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { createSupplierSchema, type CreateSupplierInput, type Supplier } from './types';
import { useCreateSupplier, useSetSupplierActive, useSuppliers, useUpdateSupplier } from './useSuppliers';

function SupplierRow({ supplier }: { supplier: Supplier }) {
  const [isEditing, setIsEditing] = useState(false);
  const updateSupplier = useUpdateSupplier();
  const setActive = useSetSupplierActive();

  const editForm = useForm<CreateSupplierInput>({
    resolver: zodResolver(createSupplierSchema),
    defaultValues: {
      name: supplier.name,
      contactEmail: supplier.contactEmail ?? '',
      phone: supplier.phone ?? '',
    },
  });

  // defaultValues n'est lu qu'au premier render de useForm : sans ce reset, ouvrir "Éditer"
  // longtemps après le montage de la ligne (ex. donnée modifiée entre-temps par un autre
  // utilisateur puis refetch React Query) préremplirait le formulaire avec des valeurs
  // périmées (bug identifié au ré-audit).
  function startEditing() {
    editForm.reset({
      name: supplier.name,
      contactEmail: supplier.contactEmail ?? '',
      phone: supplier.phone ?? '',
    });
    setIsEditing(true);
  }

  async function onSave(input: CreateSupplierInput) {
    try {
      await updateSupplier.mutateAsync({ id: supplier.id, ...input });
      setIsEditing(false);
    } catch {
      // Erreur déjà exposée via updateSupplier.isError (bannière ci-dessous) ; catch
      // uniquement pour éviter un rejet de promesse non géré (voir ré-audit).
    }
  }

  if (isEditing) {
    return (
      <form
        className="flex w-full flex-wrap items-end gap-3"
        onSubmit={editForm.handleSubmit(onSave)}
      >
        <Field label="Nom" error={editForm.formState.errors.name?.message}>
          <input className="input w-56" {...editForm.register('name')} />
        </Field>
        <Field label="Email (optionnel)" error={editForm.formState.errors.contactEmail?.message}>
          <input className="input w-56" {...editForm.register('contactEmail')} />
        </Field>
        <Field label="Téléphone (optionnel)">
          <input className="input w-40" {...editForm.register('phone')} />
        </Field>
        <button type="submit" className="btn-primary" disabled={editForm.formState.isSubmitting}>
          Enregistrer
        </button>
        <button type="button" className="btn-secondary" onClick={() => setIsEditing(false)}>
          Annuler
        </button>
        {updateSupplier.isError && (
          <p className="w-full rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600">
            {getErrorMessage(updateSupplier.error, 'Impossible de modifier le fournisseur.')}
          </p>
        )}
      </form>
    );
  }

  return (
    <>
      <div>
        <p className="font-medium text-slate-800">{supplier.name}</p>
        <p className="text-sm text-slate-500">
          {[supplier.contactEmail, supplier.phone].filter(Boolean).join(' · ') || '—'}
        </p>
      </div>
      <div className="flex items-center gap-3">
        {!supplier.isActive && (
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500">
            Inactif
          </span>
        )}
        <RequireRole role="Gestionnaire">
          <button type="button" className="btn-secondary" onClick={startEditing}>
            Éditer
          </button>
          <button
            type="button"
            className="btn-secondary"
            disabled={setActive.isPending}
            onClick={() => setActive.mutate({ id: supplier.id, isActive: !supplier.isActive })}
          >
            {supplier.isActive ? 'Désactiver' : 'Activer'}
          </button>
        </RequireRole>
      </div>
    </>
  );
}

export function SuppliersPage() {
  const { data: suppliers, isLoading, isError, error } = useSuppliers();
  const createSupplier = useCreateSupplier();

  const form = useForm<CreateSupplierInput>({ resolver: zodResolver(createSupplierSchema) });

  async function onSubmit(input: CreateSupplierInput) {
    try {
      await createSupplier.mutateAsync(input);
      form.reset();
    } catch {
      // Erreur déjà exposée via createSupplier.isError (bannière ci-dessous) ; catch
      // uniquement pour éviter un rejet de promesse non géré (voir ré-audit).
    }
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
          renderItem={(s) => <SupplierRow supplier={s} />}
        />
      )}
    </main>
  );
}
