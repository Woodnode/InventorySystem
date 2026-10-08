import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { RequireRole } from '../../routes/RequireRole';
import { CreatePanel } from '../../shared/components/CreatePanel';
import { DataList } from '../../shared/components/DataList';
import { Field } from '../../shared/components/Field';
import { PaginationControls } from '../../shared/components/PaginationControls';
import { QueryState } from '../../shared/components/QueryState';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { useToast } from '../../shared/hooks/useToast';
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
  const { notify } = useToast();

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
      notify(`Entrepôt « ${input.name} » modifié.`);
    } catch {
      // Erreur déjà exposée via updateWarehouse.isError (bannière ci-dessous) ; catch
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
        <Field label="Adresse (optionnel)">
          <input className="input sm:w-72" {...editForm.register('address')} />
        </Field>
        <div className="flex gap-2">
          <button type="submit" className="btn-primary" disabled={editForm.formState.isSubmitting}>
            Enregistrer
          </button>
          <button type="button" className="btn-secondary" onClick={() => setIsEditing(false)}>
            Annuler
          </button>
        </div>
        {updateWarehouse.isError && (
          <p role="alert" className="w-full form-error">
            {getErrorMessage(updateWarehouse.error, "Impossible de modifier l'entrepôt.")}
          </p>
        )}
      </form>
    );
  }

  return (
    <>
      <div className="min-w-0">
        <p className="font-medium text-slate-800">{warehouse.name}</p>
        {/* text-slate-600 : 7,0:1, contre 4,9:1 pour text-slate-500 sur fond blanc. */}
        {warehouse.address && <p className="text-sm text-slate-600">{warehouse.address}</p>}
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {!warehouse.isActive && (
          <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-700">
            Inactif
          </span>
        )}
        <RequireRole role="Gestionnaire">
          <button
            type="button"
            className="btn-secondary"
            aria-label={`Éditer l'entrepôt ${warehouse.name}`}
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
                { id: warehouse.id, isActive: !warehouse.isActive },
                {
                  onSuccess: () =>
                    notify(
                      `Entrepôt « ${warehouse.name} » ${warehouse.isActive ? 'désactivé' : 'activé'}.`,
                    ),
                },
              )
            }
          >
            {warehouse.isActive ? 'Désactiver' : 'Activer'}
          </button>
        </RequireRole>
      </div>
    </>
  );
}

/** Entrepots affiches par page. */
const PAR_PAGE = 12;

export function WarehousesPage() {
  const { data: warehouses, isLoading, isError, error } = useWarehouses();

  /* La liste depasse la quarantaine d'entrees et tenait sur une seule page de plus
     de trois mille pixels. L'API renvoie tout d'un coup (liste de reference bornee) :
     la recherche et la pagination se font donc cote client. */
  const [recherche, setRecherche] = useState('');
  const [page, setPage] = useState(1);

  const filtres = useMemo(() => {
    if (!warehouses) return undefined;
    const terme = recherche.trim().toLowerCase();
    if (!terme) return warehouses;
    return warehouses.filter((w) =>
      `${w.name} ${w.address ?? ''}`.toLowerCase().includes(terme),
    );
  }, [warehouses, recherche]);

  // Une recherche qui reduit la liste doit ramener a la premiere page, sinon
  // l'ecran reste vide sur une page qui n'existe plus.
  useEffect(() => {
    setPage(1);
  }, [recherche]);

  const visibles = filtres?.slice((page - 1) * PAR_PAGE, page * PAR_PAGE);
  const inactifs = warehouses?.filter((w) => !w.isActive).length ?? 0;
  const createWarehouse = useCreateWarehouse();
  const { notify } = useToast();

  const form = useForm<CreateWarehouseInput>({ resolver: zodResolver(createWarehouseSchema) });

  async function onSubmit(input: CreateWarehouseInput) {
    try {
      await createWarehouse.mutateAsync(input);
      form.reset();
      notify(`Entrepôt « ${input.name} » créé.`);
    } catch {
      // Erreur déjà exposée via createWarehouse.isError (bannière ci-dessous) ; catch
      // uniquement pour éviter un rejet de promesse non géré (voir ré-audit).
    }
  }

  return (
    <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
      <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Entrepôts</h1>

      <RequireRole role="Gestionnaire">
        <CreatePanel label="Nouvel entrepôt">
          <form
            className="flex flex-col gap-3 surface p-4 sm:flex-row sm:flex-wrap sm:items-end"
            onSubmit={form.handleSubmit(onSubmit)}
          >
            <Field label="Nom" error={form.formState.errors.name?.message}>
              <input className="input sm:w-56" {...form.register('name')} />
            </Field>
            <Field label="Adresse (optionnel)">
              <input className="input sm:w-72" {...form.register('address')} />
            </Field>
            <button type="submit" className="btn-primary" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? 'Création…' : 'Créer'}
            </button>
            {createWarehouse.isError && (
              <p role="alert" className="w-full form-error">
                {getErrorMessage(createWarehouse.error, "Impossible de créer l'entrepôt. Réessaie.")}
              </p>
            )}
          </form>
        </CreatePanel>
      </RequireRole>

      {warehouses && warehouses.length > 0 && (
        <section className="mt-6">
          <h2 className="sr-only">Recherche</h2>
          <div className="surface flex flex-col gap-1 p-4 text-sm sm:max-w-md">
            <label htmlFor="entrepot-recherche" className="font-medium text-slate-700">
              Rechercher un entrepôt
            </label>
            <input
              id="entrepot-recherche"
              type="search"
              className="input"
              placeholder="Nom ou adresse"
              value={recherche}
              onChange={(e) => setRecherche(e.target.value)}
            />
          </div>
        </section>
      )}

      {/* Le nombre d'entrepots n'etait indique nulle part. */}
      {filtres && warehouses && warehouses.length > 0 && (
        <p aria-live="polite" className="mt-4 text-sm text-slate-600">
          {filtres.length.toLocaleString('fr-CA')} entrepôt{filtres.length > 1 ? 's' : ''}
          {recherche ? ` sur ${warehouses.length}` : ''}
          {!recherche && inactifs > 0 && ` · ${inactifs} inactif${inactifs > 1 ? 's' : ''}`}
        </p>
      )}

      <QueryState
        isLoading={isLoading}
        isError={isError}
        error={error}
        isEmpty={filtres?.length === 0}
        emptyMessage={
          recherche
            ? `Aucun entrepôt ne correspond à « ${recherche} ».`
            : "Aucun entrepôt pour l'instant."
        }
        emptyAction={
          recherche ? (
            <button type="button" className="btn-secondary" onClick={() => setRecherche('')}>
              Effacer la recherche
            </button>
          ) : undefined
        }
      />

      {visibles && visibles.length > 0 && (
        <>
          <DataList
            items={visibles}
            keyOf={(w) => w.id}
            renderItem={(w) => <WarehouseRow warehouse={w} />}
          />
          <PaginationControls
            page={page}
            pageSize={PAR_PAGE}
            totalCount={filtres?.length ?? 0}
            onPageChange={setPage}
          />
        </>
      )}
    </main>
  );
}
