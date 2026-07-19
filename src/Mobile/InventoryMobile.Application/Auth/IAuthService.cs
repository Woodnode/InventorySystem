namespace InventoryMobile.Application.Auth;

/// <summary>Cas d'usage d'authentification côté mobile (implémenté dans Infrastructure via l'API).</summary>
public interface IAuthService
{
    /// <summary>Session courante en mémoire, ou null si non authentifié / pas encore restaurée.</summary>
    AuthSession? CurrentSession { get; }

    /// <summary>Relit la session persistée (SecureStorage) au démarrage de l'app.</summary>
    Task<AuthSession?> RestoreSessionAsync();

    Task<AuthSession> LoginAsync(string email, string password, CancellationToken ct = default);

    Task LogoutAsync();
}
