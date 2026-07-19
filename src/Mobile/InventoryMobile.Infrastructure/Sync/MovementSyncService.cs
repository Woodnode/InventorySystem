using InventoryMobile.Application.Auth;
using InventoryMobile.Application.Connectivity;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Persistence;
using Refit;

namespace InventoryMobile.Infrastructure.Sync;

/// <summary>Implémentation de <see cref="IMovementSyncService"/>.</summary>
public sealed class MovementSyncService : IMovementSyncService
{
    private readonly ILocalMovementQueue _queue;
    private readonly IInventoryApi _api;
    private readonly IConnectivityChecker _connectivity;
    private readonly IAuthService _authService;

    public MovementSyncService(
        ILocalMovementQueue queue,
        IInventoryApi api,
        IConnectivityChecker connectivity,
        IAuthService authService)
    {
        _queue = queue;
        _api = api;
        _connectivity = connectivity;
        _authService = authService;
    }

    public async Task<SyncResult> SyncPendingAsync(CancellationToken ct = default)
    {
        if (_authService.CurrentSession is null || !_connectivity.IsConnected)
        {
            var stillPending = await _queue.GetPendingCountAsync();
            return new SyncResult(SyncedCount: 0, RemainingPendingCount: stillPending);
        }

        var pending = await _queue.GetPendingAsync();
        var syncedCount = 0;

        foreach (var movement in pending)
        {
            ct.ThrowIfCancellationRequested();

            var request = new RecordMovementRequest(
                movement.ProductId,
                movement.WarehouseId,
                (MobileMovementType)movement.Type,
                movement.Quantity,
                movement.Reason,
                movement.ToWarehouseId,
                movement.ClientGuid);

            try
            {
                await _api.RecordMovementAsync(request, ct);
                await _queue.MarkSyncedAsync(movement.ClientGuid);
                syncedCount++;
            }
            catch (ApiException)
            {
                // Le serveur a répondu mais a rejeté CE mouvement précis (ClientGuid déjà
                // synchronisé, ou règle métier désormais violée — ex. stock insuffisant).
                // Ni l'un ni l'autre n'indique une panne réseau : on laisse ce mouvement
                // "pending" et on continue avec le suivant. Limite connue et assumée : un
                // mouvement définitivement invalide reste "pending" indéfiniment, sans UI
                // dédiée pour le consulter/purger (hors scope de ce jalon).
            }
            catch (Exception)
            {
                // Panne de transport (pas de réponse du serveur) : les mouvements suivants
                // échoueraient probablement de façon identique. On arrête ce passage ; le
                // prochain déclenchement (retour sur ProductsPage) réessaiera depuis le début.
                break;
            }
        }

        return new SyncResult(syncedCount, pending.Count - syncedCount);
    }
}
