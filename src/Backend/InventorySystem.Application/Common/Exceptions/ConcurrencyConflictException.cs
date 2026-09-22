namespace InventorySystem.Application.Common.Exceptions;

/// <summary>
/// Conflit d'écriture concurrente détecté par la couche de persistance (verrou optimiste
/// EF Core, ou violation de contrainte unique) — traduit ici depuis l'Infrastructure
/// (<c>AppDbContext.SaveChangesAsync</c>) pour que l'Api n'ait pas à connaître EF/Npgsql
/// (voir AUDIT.md B-CA1).
/// </summary>
public sealed class ConcurrencyConflictException : ApplicationException
{
    public ConcurrencyConflictException(string message) : base(message) { }
}
