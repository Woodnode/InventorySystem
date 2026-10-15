using InventorySystem.Application.Common.Interfaces;
using MediatR;

namespace InventorySystem.Application.Auth.Commands;

/// <summary>
/// Fermer une session : révoque toute la famille du refresh token présenté, pour qu'aucun
/// jeton issu de la même connexion ne puisse encore être renouvelé. Idempotent : un jeton
/// absent, inconnu ou déjà révoqué ne lève pas d'erreur (la déconnexion réussit toujours).
/// </summary>
public sealed record LogoutCommand(string? RefreshToken) : IRequest;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;

    public LogoutCommandHandler(ITokenService tokens, IRefreshTokenRepository refreshTokens)
    {
        _tokens = tokens;
        _refreshTokens = refreshTokens;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        await _refreshTokens.RevokeFamilyAsync(_tokens.HashRefreshToken(request.RefreshToken), cancellationToken);
    }
}
