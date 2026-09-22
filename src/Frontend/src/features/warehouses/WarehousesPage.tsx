import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { RequireRole } from '../../routes/RequireRole';
import { DataList } from '../../shared/components/DataList';
import { Field } from '../../shared/components/Field';
import { QueryState } from '../../shared/components/QueryState';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { createWarehouseSchema, type CreateWarehouseInput, type Warehouse } from './types';
import {
  useCreateWarehouse,
  useSetWarehouseActive,
  useUpdateWarehouse,
  useWarehouses,
} from './useWarehouses';

function WarehouseRow({ warehouse }: { warehouse: Warehouse }) {
  const [isEditing, setIsEditing] = useState(false);
  const updateWarehouse = useUpdateWarehouse();
  const setActive = useSetWarehouseActive();

  const editForm = useForm<CreateWarehouseInput>({
    resolver: zodResolver(createWarehouseSchema),
    defaultValues: { name: warehouse.name, address: warehouse.address ?? '' },
  });

  // defaultValues n'est lu qu'au premier render de useForm : sans ce reset, ouvrir "Éditer"
  // longtemps après le montage de la ligne (donnée modifiée entre-temps par un autre
  // utilisateur puis refetch React Query) préremplirait le formulaire avec des valeurs
  // périmées (bug identifié au ré-audit).
  function startEditing() {
    editForm.reset({ name: warehouse.name, address: warehouse.address ?? '' });
    setIsEditing(true);
  }

  async function onSave(input: CreateWarehouseInput) {
    try {
      await updateWarehouse.mutateAsync({ id: warehouse.id, ...input });
      setIsEditing(false);
    } catch {
      // Erreur déjà exposée via updateWarehouse.isError (bannière ci-dessous) ; catch
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
        <Field label="Adresse (optionnel)">
          <input className="input w-72" {...editForm.register('address')} />
        </Field>
        <button type="submit" className="btn-primary" disabled={editForm.formState.isSubmitting}>
          Enregistrer
        </button>
        <button type="button" className="btn-secondary" onClick={() => setIsEditing(false)}>
          Annuler
        </button>
        {updateWarehouse.isError && (
          <p className="w-full rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600">
            {getErrorMessage(updateWarehouse.error, "Impossible de modifier l'entrepôt.")}
          </p>
        )}
      </form>
    );
  }

  return (
    <>
      <div>
        <p className="font-medium text-slate-800">{warehouse.name}</p>
        {warehouse.address && <p className="text-sm text-slate-500">{warehouse.address}</p>}
      </div>
      <div className="flex items-center gap-3">
        {!warehouse.isActive && (
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
            onClick={() => setActive.mutate({ id: warehouse.id, isActive: !warehouse.isActive })}
          >
            {warehouse.isActive ? 'Désactiver' : 'Activer'}
          </button>
        </RequireRole>
      </div>
    </>
  );
}

export function WarehousesPage() {
  const { data: warehouses, isLoading, isError, error } = useWarehouses();
  const createWarehouse = useCreateWarehouse();

  const form = useForm<CreateWarehouseInput>({ resolver: zodResolver(createWarehouseSchema) });

  async function onSubmit(input: CreateWarehouseInput) {
    try {
      await createWarehouse.mutateAsync(input);
      form.reset();
    } catch {
      // Erreur déjà exposée via createWarehouse.isError (bannière ci-dessous) ; catch
      // uniquement pour éviter un rejet de promesse non géré (voir ré-audit).
    }
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
          renderItem={(w) => <WarehouseRow warehouse={w} />}
        />
      )}
    </main>
  );
}
