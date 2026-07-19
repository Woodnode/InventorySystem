using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Auth;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db) => _db = db;

    public async Task StoreAsync(Guid userId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct = default)
        => await _db.RefreshTokens.AddAsync(RefreshToken.Create(userId, tokenHash, expiresAtUtc), ct);

    public async Task<Guid?> GetActiveUserIdAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        return token is { IsActive: true } ? token.UserId : null;
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        token?.Revoke();
    }
}
