namespace InventorySystem.Application.Common.Exceptions;

/// <summary>Échec d'authentification / session (identifiants, refresh invalide, reuse).</summary>
public sealed class AuthenticationException : ApplicationException
{
    public AuthenticationException(string message) : base(message) { }
}
