namespace InventorySystem.Domain.Exceptions;

/// <summary>
/// Levée lorsqu'un mouvement de stock est invalide (quantité nulle/négative, etc.).
/// </summary>
public sealed class InvalidMovementException : DomainException
{
    public InvalidMovementException(string message) : base(message) { }
}
