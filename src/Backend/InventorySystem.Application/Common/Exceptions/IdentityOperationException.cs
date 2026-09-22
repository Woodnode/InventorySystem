namespace InventorySystem.Application.Common.Exceptions;

/// <summary>Échec d'une opération Identity (création de compte, etc.) — pas une règle Domain.</summary>
public sealed class IdentityOperationException : ApplicationException
{
    public IdentityOperationException(string message) : base(message) { }
}
