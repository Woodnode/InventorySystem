namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Valide l'ensemble des changements d'une transaction en une seule opération.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
