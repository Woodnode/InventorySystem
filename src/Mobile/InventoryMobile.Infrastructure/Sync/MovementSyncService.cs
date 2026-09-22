using System.Net;
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
        var discardedCount = 0;

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
            catch (ApiException ex) when (
                ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Session invalide : arrêter tout de suite (AuthHeaderHandler redirige au login).
                break;
            }
            catch (ApiException ex) when (
                ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                // Rejet métier définitif (ou ClientGuid déjà synchronisé) : retirer de la file
                // pour éviter une boucle « poison » indéfinie.
                await _queue.MarkSyncedAsync(movement.ClientGuid);
                discardedCount++;
            }
            catch (ApiException)
            {
                // Autre erreur HTTP (5xx…) : laisser pending et continuer avec le suivant.
            }
            catch (Exception)
            {
                // Panne de transport : arrêter ce passage.
                break;
            }
        }

        var remaining = pending.Count - syncedCount - discardedCount;
        return new SyncResult(syncedCount, remaining);
    }
}
