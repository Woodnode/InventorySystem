namespace InventorySystem.Domain.Exceptions;

/// <summary>
/// Levée lorsqu'un mouvement de sortie tenterait de rendre le stock négatif.
/// </summary>
public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(int available, int requested)
        : base($"Stock insuffisant : {available} disponible(s), {requested} demandé(s).")
    {
    }
}
