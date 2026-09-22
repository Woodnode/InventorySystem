namespace InventorySystem.Application.Common.Exceptions;

/// <summary>
/// Exception de couche Application (cas d'usage) — distincte de
/// <see cref="Domain.Exceptions.DomainException"/> (invariants métier du Domain).
/// </summary>
public class ApplicationException : Exception
{
    public ApplicationException(string message) : base(message) { }
}
