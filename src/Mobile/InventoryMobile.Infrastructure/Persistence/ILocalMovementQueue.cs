namespace InventoryMobile.Infrastructure.Persistence;

/// <summary>
/// File d'attente locale des mouvements créés hors-ligne (pattern Outbox).
/// </summary>
public interface ILocalMovementQueue
{
    Task EnqueueAsync(LocalMovement movement);

    /// <summary>Mouvements pas encore confirmés par le serveur, du plus ancien au plus récent.</summary>
    Task<IReadOnlyList<LocalMovement>> GetPendingAsync();

    Task MarkSyncedAsync(Guid clientGuid);

    Task<int> GetPendingCountAsync();
}
