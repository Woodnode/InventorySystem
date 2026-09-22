using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Application.Common.Interfaces;

namespace InventorySystem.Application.Auth.Commands;

/// <summary>
/// Fabrique interne partagée Register/Login/Refresh (DRY).
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
        IdentityUserInfo? knownUser = null,
        Guid? familyId = null,
        Guid? replacesTokenId = null)
    {
        var user = knownUser ?? await identity.FindByIdAsync(userId, ct)
            ?? throw new AuthenticationException("Utilisateur introuvable.");

        var accessToken = tokens.GenerateAccessToken(user.Id, user.Email, user.Roles);
        var refreshToken = tokens.GenerateRefreshToken();
        var refreshTokenHash = tokens.HashRefreshToken(refreshToken);
        var resolvedFamilyId = familyId ?? Guid.NewGuid();

        await refreshTokens.StoreAsync(
            user.Id,
            refreshTokenHash,
            tokens.GetRefreshTokenExpiryUtc(),
            resolvedFamilyId,
            replacesTokenId,
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthResultDto(
            accessToken.Token, accessToken.ExpiresAtUtc, refreshToken, user.Email, user.DisplayName, user.Roles);
    }
}
