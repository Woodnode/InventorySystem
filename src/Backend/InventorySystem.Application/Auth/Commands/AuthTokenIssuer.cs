using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Application.Auth.Commands;

/// <summary>
/// Fabrique interne partagée par Register/Login/Refresh pour émettre un access token
/// + un refresh token de façon identique (évite la duplication entre les trois handlers — SRP/DRY).
/// </summary>
internal static class AuthTokenIssuer
{
    public static async Task<AuthResultDto> IssueAsync(
        IIdentityService identity,
        ITokenService tokens,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        Guid userId,
        CancellationToken ct,
        IdentityUserInfo? knownUser = null)
    {
        var user = knownUser ?? await identity.FindByIdAsync(userId, ct)
            ?? throw new DomainException("Utilisateur introuvable.");

        var accessToken = tokens.GenerateAccessToken(user.Id, user.Email, user.Roles);
        var refreshToken = tokens.GenerateRefreshToken();
        var refreshTokenHash = tokens.HashRefreshToken(refreshToken);

        await refreshTokens.StoreAsync(user.Id, refreshTokenHash, tokens.GetRefreshTokenExpiryUtc(), ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthResultDto(
            accessToken.Token, accessToken.ExpiresAtUtc, refreshToken, user.Email, user.DisplayName, user.Roles);
    }
}
