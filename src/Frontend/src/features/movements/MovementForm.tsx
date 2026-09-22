import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { useWarehouses } from '../warehouses/useWarehouses';
import { recordMovementSchema, type RecordMovementInput } from './types';
import { useRecordMovement } from './useMovements';

/**
 * Formulaire d'enregistrement d'un mouvement (In/Out/Transfer). Réutilisé tel quel par
 * le mobile via le même contrat RecordMovementCommand (voir plan §9 : API consommée à
 * l'identique par le web et le mobile).
 */
export function MovementForm({ productId }: { productId: string }) {
  const { data: warehouses } = useWarehouses();
  // Un entrepôt désactivé ne doit plus pouvoir recevoir de nouveaux mouvements — sinon
  // "désactiver" (plutôt que supprimer) ne protège rien (voir ré-audit). L'historique, lui,
  // continue de résoudre les noms des entrepôts inactifs via warehouseNameLookup, non filtré.
  const activeWarehouses = warehouses?.filter((w) => w.isActive);
  const recordMovement = useRecordMovement();

  const form = useForm<RecordMovementInput>({
    resolver: zodResolver(recordMovementSchema),
    defaultValues: { productId, type: 'In', quantity: 1 },
  });

  const type = form.watch('type');

  async function onSubmit(input: RecordMovementInput) {
    try {
      await recordMovement.mutateAsync({ ...input, productId });
      // On garde productId + type (permet d'enchaîner plusieurs saisies du même type sans
      // rescroller à chaque fois) ; tout le reste doit être explicitement resaisi pour éviter
      // de réutiliser silencieusement un entrepôt ou un motif périmé (form.reset() ne
      // réinitialise que les clés qu'on lui passe, voir ProductsPage pour le même écueil).
      form.reset({
        productId,
        type: input.type,
        quantity: 1,
        warehouseId: '',
        toWarehouseId: '',
        reason: '',
      });
    } catch {
      // Erreur déjà exposée via recordMovement.isError (bannière ci-dessous) ; catch
      // uniquement pour éviter un rejet de promesse non géré (voir ré-audit).
    }
  }

  return (
    <form
      className="grid grid-cols-2 gap-3 rounded-xl border border-slate-200 bg-white p-4 sm:grid-cols-4"
      onSubmit={form.handleSubmit(onSubmit)}
    >
      <label className="flex flex-col gap-1 text-sm">
        <span className="font-medium text-slate-700">Type</span>
        <select className="input" {...form.register('type')}>
          <option value="In">Entrée</option>
          <option value="Out">Sortie</option>
          <option value="Transfer">Transfert</option>
        </select>
      </label>

      <label className="flex flex-col gap-1 text-sm">
        <span className="font-medium text-slate-700">
          {type === 'Transfer' ? 'Entrepôt source' : 'Entrepôt'}
        </span>
        <select className="input" {...form.register('warehouseId')}>
          <option value="">— choisir —</option>
          {activeWarehouses?.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name}
            </option>
          ))}
        </select>
        {form.formState.errors.warehouseId && (
          <span className="text-xs text-rose-600">{form.formState.errors.warehouseId.message}</span>
        )}
      </label>

      {type === 'Transfer' && (
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-slate-700">Entrepôt destination</span>
          <select className="input" {...form.register('toWarehouseId')}>
            <option value="">— choisir —</option>
            {activeWarehouses?.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
          {form.formState.errors.toWarehouseId && (
            <span className="text-xs text-rose-600">
              {form.formState.errors.toWarehouseId.message}
            </span>
          )}
        </label>
      )}

      <label className="flex flex-col gap-1 text-sm">
        <span className="font-medium text-slate-700">Quantité</span>
        <input
          type="number"
          min={1}
          className="input"
          {...form.register('quantity', { valueAsNumber: true })}
        />
        {form.formState.errors.quantity && (
          <span className="text-xs text-rose-600">{form.formState.errors.quantity.message}</span>
        )}
      </label>

      <label className="col-span-2 flex flex-col gap-1 text-sm sm:col-span-3">
        <span className="font-medium text-slate-700">Motif (optionnel)</span>
        <input className="input" {...form.register('reason')} />
      </label>

      <div className="flex items-end">
        <button type="submit" className="btn-primary w-full" disabled={form.formState.isSubmitting}>
          Enregistrer
        </button>
      </div>

      {recordMovement.isError && (
        <p className="col-span-full text-sm text-rose-600">
          {getErrorMessage(recordMovement.error, 'Mouvement refusé. Réessaie.')}
        </p>
      )}
    </form>
  );
}
