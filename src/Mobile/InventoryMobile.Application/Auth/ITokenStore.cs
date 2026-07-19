namespace InventoryMobile.Application.Auth;

/// <summary>
/// Abstraction du stockage sécurisé de la session (implémentée par le head MAUI via
/// <c>SecureStorage</c> — voir plan §8.4). L'Infrastructure ne connaît pas la plateforme,
/// seulement ce contrat (Dependency Inversion).
/// </summary>
public interface ITokenStore
{
    Task SaveAsync(AuthSession session);
    Task<AuthSession?> LoadAsync();
    Task ClearAsync();
}
