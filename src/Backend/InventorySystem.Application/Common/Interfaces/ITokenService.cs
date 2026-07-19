namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Génération des jetons d'authentification. Implémentée dans Infrastructure (DIP) :
/// l'Application ne connaît ni JWT ni la clé de signature, seulement ce contrat.
/// </summary>
public interface ITokenService
{
    GeneratedAccessToken GenerateAccessToken(Guid userId, string email, IReadOnlyList<string> roles);

    /// <summary>Jeton opaque aléatoire (pas un JWT) — voir plan §6.</summary>
    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);

    /// <summary>
    /// Date d'expiration à appliquer à un refresh token nouvellement émis
    /// (pilotée par la configuration Jwt:RefreshTokenDays côté Infrastructure).
    /// </summary>
    DateTime GetRefreshTokenExpiryUtc();
}

public sealed record GeneratedAccessToken(string Token, DateTime ExpiresAtUtc);
