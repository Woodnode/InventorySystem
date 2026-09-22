using InventorySystem.Application.Auth.Dtos;

namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Résultat d'une consommation atomique réussie d'un refresh token.
/// </summary>
public sealed record RefreshTokenConsumeResult(Guid UserId, Guid TokenId, Guid FamilyId);

/// <summary>
/// Persistance des refresh tokens. Le type concret Infrastructure n'est pas exposé (DIP).
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Persiste un nouveau jeton. Si <paramref name="replacesTokenId"/> est fourni,
    /// lie l'ancien jeton via <c>ReplacedByTokenId</c>.
    /// </summary>
    Task StoreAsync(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        Guid familyId,
        Guid? replacesTokenId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Consomme atomiquement un jeton actif. Null si déjà consommé / expiré / inconnu.
    /// </summary>
    Task<RefreshTokenConsumeResult?> TryConsumeActiveAsync(
        string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Réutilisation hors fenêtre de grâce : révoque toute la <c>FamilyId</c> et renvoie true.
    /// </summary>
    Task<bool> TryHandleReuseAsync(string tokenHash, CancellationToken ct = default);
}

/// <summary>
/// Cache courte durée du résultat de rotation : un refresh concurrent perdant
/// reçoit le même couple de jetons que le gagnant (B-H6) sans stocker le plaintext en base.
/// </summary>
public interface IRefreshRotationCache
{
    void Store(string consumedTokenHash, AuthResultDto result);
    bool TryGet(string consumedTokenHash, out AuthResultDto? result);

    /// <summary>
    /// Retire une entrée. Utilisé quand le commit qui a produit le résultat mis en cache
    /// échoue finalement (voir B-H6r) : évite d'exposer à un perdant concurrent un couple
    /// de jetons qui n'a en réalité jamais été persisté.
    /// </summary>
    void Remove(string consumedTokenHash);
}
