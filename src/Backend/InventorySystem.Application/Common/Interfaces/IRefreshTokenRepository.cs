namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Persistance des refresh tokens. Le type concret (Infrastructure.Auth.RefreshToken)
/// n'est pas exposé ici pour ne pas faire fuiter un détail Infrastructure dans l'Application ;
/// on manipule uniquement les informations nécessaires aux cas d'usage Auth.
/// </summary>
public interface IRefreshTokenRepository
{
    Task StoreAsync(Guid userId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct = default);

    /// <summary>Renvoie l'utilisateur propriétaire du jeton s'il est valide (non expiré, non révoqué).</summary>
    Task<Guid?> GetActiveUserIdAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Révoque le jeton (rotation : un refresh token n'est utilisable qu'une fois).</summary>
    Task RevokeAsync(string tokenHash, CancellationToken ct = default);
}
