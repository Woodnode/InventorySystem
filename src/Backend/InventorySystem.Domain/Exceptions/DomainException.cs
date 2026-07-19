namespace InventorySystem.Domain.Exceptions;

/// <summary>
/// Exception de base pour toute violation d'une règle métier du domaine.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
