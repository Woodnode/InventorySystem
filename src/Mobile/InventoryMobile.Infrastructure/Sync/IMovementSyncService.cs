namespace InventoryMobile.Infrastructure.Sync;

/// <summary>Résultat d'une tentative de synchronisation de la file d'attente locale.</summary>
public sealed record SyncResult(int SyncedCount, int RemainingPendingCount);

/// <summary>
/// Rejoue les mouvements mis en file d'attente localement (offline) vers le serveur,
/// dès que la connectivité et l'authentification le permettent.
/// </summary>
public interface IMovementSyncService
{
    Task<SyncResult> SyncPendingAsync(CancellationToken ct = default);
}
