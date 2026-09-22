namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Valide l'ensemble des changements d'une transaction en une seule opération.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Exécute <paramref name="action"/> dans une transaction DB explicite
    /// (commit si succès, rollback sinon).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
