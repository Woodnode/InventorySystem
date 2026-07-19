namespace InventoryMobile.Application.Auth;

/// <summary>
/// Session authentifiée persistée localement (miroir de AuthResultDto côté backend).
/// Reconstruite au démarrage de l'app via <see cref="ITokenStore"/> pour éviter de
/// redemander les identifiants à chaque lancement.
/// </summary>
public sealed record AuthSession(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles)
{
    /// <summary>Marge de sécurité pour rafraîchir avant l'expiration réelle, pas après.</summary>
    public bool IsExpiredOrExpiringSoon(TimeSpan margin) =>
        DateTime.UtcNow >= AccessTokenExpiresAtUtc - margin;
}
