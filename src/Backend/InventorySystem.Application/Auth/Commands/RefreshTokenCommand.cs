using FluentValidation;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Exceptions;
using MediatR;

namespace InventorySystem.Application.Auth.Commands;

/// <summary>
/// Cas d'usage : renouveler un access token à partir d'un refresh token valide,
/// sans reconnexion — utilisé par le mobile pour les sessions longues (plan §6).
/// Rotation : l'ancien refresh token est révoqué et remplacé à chaque appel.
/// </summary>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResultDto>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    private readonly IIdentityService _identity;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IIdentityService identity,
        ITokenService tokens,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork)
    {
        _identity = identity;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokens.HashRefreshToken(request.RefreshToken);

        var userId = await _refreshTokens.GetActiveUserIdAsync(tokenHash, cancellationToken)
            ?? throw new DomainException("Refresh token invalide ou expiré.");

        // Rotation : ce jeton ne sera plus jamais réutilisable, même s'il est intercepté après coup.
        await _refreshTokens.RevokeAsync(tokenHash, cancellationToken);

        return await AuthTokenIssuer.IssueAsync(
            _identity, _tokens, _refreshTokens, _unitOfWork, userId, cancellationToken);
    }
}
