using FluentValidation;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Application.Common.Interfaces;
using MediatR;

namespace InventorySystem.Application.Auth.Commands;

/// <summary>
/// Renouveler access + refresh. Consume atomique + famille de jetons ;
/// perdant concurrent → même résultat via <see cref="IRefreshRotationCache"/>.
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
    private readonly IRefreshRotationCache _rotationCache;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IIdentityService identity,
        ITokenService tokens,
        IRefreshTokenRepository refreshTokens,
        IRefreshRotationCache rotationCache,
        IUnitOfWork unitOfWork)
    {
        _identity = identity;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _rotationCache = rotationCache;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokens.HashRefreshToken(request.RefreshToken);
        AuthResultDto? result = null;
        var cached = false;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var consumed = await _refreshTokens.TryConsumeActiveAsync(tokenHash, ct);
                if (consumed is null)
                    throw new RefreshTokenMissException();

                result = await AuthTokenIssuer.IssueAsync(
                    _identity, _tokens, _refreshTokens, _unitOfWork, consumed.UserId, ct,
                    familyId: consumed.FamilyId,
                    replacesTokenId: consumed.TokenId);

                // Peuplé AVANT le commit (pas après ExecuteInTransactionAsync) : un perdant
                // concurrent qui échoue son TryConsumeActiveAsync se heurte au verrou de ligne
                // posé par ce Handle ci-dessus, donc il ne reprend qu'une fois CE commit terminé
                // — le cache est alors déjà peuplé. Le peupler après coup laissait une fenêtre
                // où le perdant pouvait rater le cache et se faire rejeter à tort (B-H6r).
                _rotationCache.Store(tokenHash, result);
                cached = true;
            }, cancellationToken);
        }
        catch (RefreshTokenMissException)
        {
            // Perdant d'une course concurrente : même couple que le gagnant.
            if (_rotationCache.TryGet(tokenHash, out var winnerResult) && winnerResult is not null)
                return winnerResult;

            // Fenêtre de grâce de 30s (voir MemoryRefreshRotationCache) : un rejeu du même
            // token dans cette fenêtre est traité comme une course concurrente légitime, pas
            // comme un vol — TryHandleReuseAsync ne révoque la famille que hors fenêtre. C'est
            // un compromis assumé (tolérer un double-appel réseau/StrictMode au prix d'une
            // courte fenêtre de rejeu non détecté) — voir AUDIT.md B-H6r2.
            if (await _refreshTokens.TryHandleReuseAsync(tokenHash, cancellationToken))
                throw new AuthenticationException(
                    "Refresh token réutilisé : la session (famille de jetons) a été révoquée.");

            throw new AuthenticationException("Refresh token invalide ou expiré.");
        }
        catch
        {
            // Le commit a échoué après le Store optimiste ci-dessus (ex. conflit DB) : ne pas
            // laisser un couple de jetons non persisté visible aux requêtes concurrentes.
            if (cached)
                _rotationCache.Remove(tokenHash);
            throw;
        }

        return result!;
    }

    private sealed class RefreshTokenMissException : Exception;
}
