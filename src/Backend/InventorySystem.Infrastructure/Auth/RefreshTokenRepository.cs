using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Auth;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    /// <summary>
    /// Fenêtre pendant laquelle un refresh concurrent n'est pas traité comme vol.
    /// Complétée par <see cref="IRefreshRotationCache"/> pour renvoyer le même résultat.
    /// </summary>
    private static readonly TimeSpan ConcurrentGrace = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _db;
    private readonly IRefreshRotationCache _rotationCache;

    public RefreshTokenRepository(AppDbContext db, IRefreshRotationCache rotationCache)
    {
        _db = db;
        _rotationCache = rotationCache;
    }

    public async Task StoreAsync(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        Guid familyId,
        Guid? replacesTokenId = null,
        CancellationToken ct = default)
    {
        var entity = RefreshToken.Create(userId, tokenHash, expiresAtUtc, familyId);
        await _db.RefreshTokens.AddAsync(entity, ct);

        if (replacesTokenId is Guid oldId)
        {
            await _db.RefreshTokens
                .Where(t => t.Id == oldId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.ReplacedByTokenId, entity.Id),
                    ct);
        }
    }

    public async Task<RefreshTokenConsumeResult?> TryConsumeActiveAsync(
        string tokenHash, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var token = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.RevokedAtUtc == null && t.ExpiresAtUtc > now,
                ct);

        if (token is null)
            return null;

        var affected = await _db.RefreshTokens
            .Where(t => t.Id == token.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

        return affected == 1
            ? new RefreshTokenConsumeResult(token.UserId, token.Id, token.FamilyId)
            : null;
    }

    public async Task<bool> TryHandleReuseAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token?.RevokedAtUtc is null)
            return false;

        var age = DateTime.UtcNow - token.RevokedAtUtc.Value;
        if (age < ConcurrentGrace)
        {
            // Perdant concurrent : ne pas révoquer la famille (le cache renvoie le résultat gagnant).
            return false;
        }

        // Replay hors fenêtre → compromission : révoquer toute la chaîne de rotation.
        var now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.FamilyId == token.FamilyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

        return true;
    }

    public async Task RevokeFamilyAsync(string tokenHash, CancellationToken ct = default)
    {
        var familyId = await _db.RefreshTokens
            .AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId is null)
            return;

        var now = DateTime.UtcNow;

        // Les jetons de la famille consommés depuis moins de ConcurrentGrace peuvent encore
        // avoir une rotation en cache : sans ce nettoyage, rejouer l'un d'eux juste après la
        // déconnexion renvoyait le couple de jetons mis en cache, comme si de rien n'était.
        var graceStart = now - ConcurrentGrace;
        var recentlyConsumed = await _db.RefreshTokens
            .AsNoTracking()
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc != null && t.RevokedAtUtc >= graceStart)
            .Select(t => t.TokenHash)
            .ToListAsync(ct);

        foreach (var consumedHash in recentlyConsumed)
            _rotationCache.Remove(consumedHash);

        await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }
}
