namespace InventoryMobile.Application.Auth;

/// <summary>Cas d'usage d'authentification côté mobile (implémenté dans Infrastructure via l'API).</summary>
public interface IAuthService
{
    /// <summary>Session courante en mémoire, ou null si non authentifié / pas encore restaurée.</summary>
    AuthSession? CurrentSession { get; }

    /// <summary>
    /// Aligne <see cref="CurrentSession"/> sur une session déjà persistée (ex. après refresh
    /// token dans AuthHeaderHandler) sans repasser par SecureStorage.
    /// </summary>
    void ApplySession(AuthSession? session);

    /// <summary>Relit la session persistée (SecureStorage) au démarrage de l'app.</summary>
    Task<AuthSession?> RestoreSessionAsync();

    Task<AuthSession> LoginAsync(string email, string password, CancellationToken ct = default);

    Task LogoutAsync();
}
