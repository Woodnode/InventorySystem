namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Refresh token opaque. Rotation : un jeton actif est révoqué et remplacé ;
/// <see cref="FamilyId"/> regroupe la chaîne de rotation (révocation ciblée si vol).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    /// <summary>Identifiant de la chaîne login → rotations successives.</summary>
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; private set; }
    /// <summary>Jeton émis en remplacement lors de la rotation (null si encore actif / login).</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId, string tokenHash, DateTime expiresAtUtc, Guid familyId)
        => new()
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
        };

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;
}
