import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { RequireRole } from '../../routes/RequireRole';
import { CreatePanel } from '../../shared/components/CreatePanel';
import { DataList } from '../../shared/components/DataList';
import { Field } from '../../shared/components/Field';
import { QueryState } from '../../shared/components/QueryState';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { useToast } from '../../shared/hooks/useToast';
import { createSupplierSchema, type CreateSupplierInput, type Supplier } from './types';
import { useCreateSupplier, useSetSupplierActive, useSuppliers, useUpdateSupplier } from './useSuppliers';

function SupplierRow({ supplier }: { supplier: Supplier }) {
  const [isEditing, setIsEditing] = useState(false);
  const updateSupplier = useUpdateSupplier();
  const setActive = useSetSupplierActive();
  const { notify } = useToast();

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
      notify(`Fournisseur « ${input.name} » modifié.`);
    } catch {
      // Erreur déjà exposée via updateSupplier.isError (bannière ci-dessous) ; catch
      // uniquement pour éviter un rejet de promesse non géré (voir ré-audit).
    }
  }

  if (isEditing) {
    return (
      <form
        className="flex w-full flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end"
        onSubmit={editForm.handleSubmit(onSave)}
      >
        <Field label="Nom" error={editForm.formState.errors.name?.message}>
          <input className="input sm:w-56" {...editForm.register('name')} />
        </Field>
        <Field label="Email (optionnel)" error={editForm.formState.errors.contactEmail?.message}>
          <input className="input sm:w-56" {...editForm.register('contactEmail')} />
        </Field>
        <Field label="Téléphone (optionnel)">
          <input className="input sm:w-40" {...editForm.register('phone')} />
        </Field>
        <div className="flex gap-2">
          <button type="submit" className="btn-primary" disabled={editForm.formState.isSubmitting}>
            Enregistrer
          </button>
          <button type="button" className="btn-secondary" onClick={() => setIsEditing(false)}>
            Annuler
          </button>
        </div>
        {updateSupplier.isError && (
          <p role="alert" className="w-full form-error">
            {getErrorMessage(updateSupplier.error, 'Impossible de modifier le fournisseur.')}
          </p>
        )}
      </form>
    );
  }

  return (
    <>
      <div className="min-w-0">
        <p className="font-medium text-slate-800">{supplier.name}</p>
        {/* text-slate-600 : 7,0:1, contre 4,9:1 pour text-slate-500. */}
        <p className="text-sm text-slate-600">
          {[supplier.contactEmail, supplier.phone].filter(Boolean).join(' · ') || '—'}
        </p>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {!supplier.isActive && (
          <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-700">
            Inactif
          </span>
        )}
        <RequireRole role="Gestionnaire">
          <button
            type="button"
            className="btn-secondary"
            aria-label={`Éditer le fournisseur ${supplier.name}`}
            onClick={startEditing}
          >
            Éditer
          </button>
          <button
            type="button"
            className="btn-secondary"
            disabled={setActive.isPending}
            onClick={() =>
              setActive.mutate(
                { id: supplier.id, isActive: !supplier.isActive },
                {
                  onSuccess: () =>
                    notify(
                      `Fournisseur « ${supplier.name} » ${supplier.isActive ? 'désactivé' : 'activé'}.`,
                    ),
                },
              )
            }
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
  const { notify } = useToast();

  const form = useForm<CreateSupplierInput>({ resolver: zodResolver(createSupplierSchema) });

  async function onSubmit(input: CreateSupplierInput) {
    try {
      await createSupplier.mutateAsync(input);
      form.reset();
      notify(`Fournisseur « ${input.name} » créé.`);
    } catch {
      // Erreur déjà exposée via createSupplier.isError (bannière ci-dessous) ; catch
      // uniquement pour éviter un rejet de promesse non géré (voir ré-audit).
    }
  }

  return (
    <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
      <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Fournisseurs</h1>

      <RequireRole role="Gestionnaire">
        <CreatePanel label="Nouveau fournisseur">
          <form
            className="flex flex-col gap-3 surface p-4 sm:flex-row sm:flex-wrap sm:items-end"
            onSubmit={form.handleSubmit(onSubmit)}
          >
            <Field label="Nom" error={form.formState.errors.name?.message}>
              <input className="input sm:w-56" {...form.register('name')} />
            </Field>
            <Field label="Email (optionnel)" error={form.formState.errors.contactEmail?.message}>
              <input className="input sm:w-56" {...form.register('contactEmail')} />
            </Field>
            <Field label="Téléphone (optionnel)">
              <input className="input sm:w-40" {...form.register('phone')} />
            </Field>
            <button type="submit" className="btn-primary" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? 'Création…' : 'Créer'}
            </button>
            {createSupplier.isError && (
              <p role="alert" className="w-full form-error">
                {getErrorMessage(createSupplier.error, 'Impossible de créer le fournisseur. Réessaie.')}
              </p>
            )}
          </form>
        </CreatePanel>
      </RequireRole>

      <QueryState
        isLoading={isLoading}
        isError={isError}
        error={error}
        isEmpty={suppliers?.length === 0}
        emptyMessage="Aucun fournisseur pour l'instant."
        /* Un etat vide qui ne dit pas comment le remplir laisse l'ecran sans issue. */
        emptyAction={
          <RequireRole role="Gestionnaire">
            <p className="text-sm text-slate-600">
              Utilise « Nouveau fournisseur » ci-dessus pour en enregistrer un.
            </p>
          </RequireRole>
        }
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
