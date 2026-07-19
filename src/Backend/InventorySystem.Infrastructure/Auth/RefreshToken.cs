namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Refresh token opaque, à durée de vie longue, permettant de renouveler un access
/// token JWT sans reconnexion (plan §6). Un seul jeton actif est révoqué à la fois
/// remplacé (rotation), pour limiter le risque de rejeu si volé.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAtUtc)
        => new()
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc
        };

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;
}
