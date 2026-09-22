using FluentAssertions;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Application.Common.Interfaces;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Auth;

/// <summary>
/// Couvre la rotation de refresh token en isolation (Moq, sans base de données) — en
/// particulier B-H6r (fenêtre de course sur le cache de rotation) et B-H6r2 (sémantique du
/// rejeu dans la fenêtre de grâce de 30s), tous deux corrigés dans <see cref="RefreshTokenCommand"/>.
/// </summary>
public sealed class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IRefreshRotationCache> _rotationCache = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private const string RawToken = "raw-refresh-token";
    private const string TokenHash = "hashed-refresh-token";
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tokenId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();

    public RefreshTokenCommandHandlerTests()
    {
        _tokens.Setup(t => t.HashRefreshToken(RawToken)).Returns(TokenHash);
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns("new-raw-refresh-token");
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns(new GeneratedAccessToken("new-access-token", DateTime.UtcNow.AddMinutes(15)));
        _tokens.Setup(t => t.GetRefreshTokenExpiryUtc()).Returns(DateTime.UtcNow.AddDays(7));

        _identity.Setup(i => i.FindByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUserInfo(_userId, "user@test.local", "Jean Dupont", new[] { "Employe" }));

        // Simule une vraie transaction réussie : exécute simplement le délégué.
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));
    }

    private RefreshTokenCommandHandler CreateHandler()
        => new(_identity.Object, _tokens.Object, _refreshTokens.Object, _rotationCache.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_WithActiveToken_ConsumesAndStoresResultInCacheBeforeReturning()
    {
        _refreshTokens
            .Setup(r => r.TryConsumeActiveAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenConsumeResult(_userId, _tokenId, _familyId));

        var result = await CreateHandler().Handle(new RefreshTokenCommand(RawToken), CancellationToken.None);

        result.AccessToken.Should().Be("new-access-token");
        // B-H6r : le cache doit être peuplé (donc AVANT que Handle ne rende la main), pas
        // seulement après coup — sinon un perdant concurrent pourrait rater le cache.
        _rotationCache.Verify(c => c.Store(TokenHash, It.Is<AuthResultDto>(r => r.AccessToken == "new-access-token")), Times.Once);
    }

    [Fact]
    public async Task Handle_ConcurrentLoser_ReturnsSameResultAsWinnerFromCache()
    {
        // Le token a déjà été consommé par le "gagnant" de la course : TryConsumeActiveAsync
        // échoue pour ce perdant.
        _refreshTokens
            .Setup(r => r.TryConsumeActiveAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenConsumeResult?)null);

        var winnerResult = new AuthResultDto(
            "winner-access", DateTime.UtcNow.AddMinutes(15), "winner-refresh",
            "user@test.local", "Jean Dupont", new[] { "Employe" });
        _rotationCache.Setup(c => c.TryGet(TokenHash, out winnerResult)).Returns(true);

        var result = await CreateHandler().Handle(new RefreshTokenCommand(RawToken), CancellationToken.None);

        result.Should().BeEquivalentTo(winnerResult);
        // Un perdant concurrent ne doit jamais déclencher la détection de réutilisation.
        _refreshTokens.Verify(r => r.TryHandleReuseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReuseOutsideGraceWindow_RevokesFamilyAndThrows()
    {
        _refreshTokens
            .Setup(r => r.TryConsumeActiveAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenConsumeResult?)null);
        AuthResultDto? nothingCached = null;
        _rotationCache.Setup(c => c.TryGet(TokenHash, out nothingCached)).Returns(false);
        _refreshTokens
            .Setup(r => r.TryHandleReuseAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // hors fenêtre de grâce -> vol détecté, famille révoquée

        var act = async () => await CreateHandler().Handle(new RefreshTokenCommand(RawToken), CancellationToken.None);

        (await act.Should().ThrowAsync<AuthenticationException>())
            .WithMessage("*révoquée*");
    }

    [Fact]
    public async Task Handle_UnknownOrExpiredToken_ThrowsGenericAuthenticationException()
    {
        _refreshTokens
            .Setup(r => r.TryConsumeActiveAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenConsumeResult?)null);
        AuthResultDto? nothingCached = null;
        _rotationCache.Setup(c => c.TryGet(TokenHash, out nothingCached)).Returns(false);
        _refreshTokens
            .Setup(r => r.TryHandleReuseAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // ni actif, ni une réutilisation détectable (jamais existé / déjà expiré)

        var act = async () => await CreateHandler().Handle(new RefreshTokenCommand(RawToken), CancellationToken.None);

        (await act.Should().ThrowAsync<AuthenticationException>())
            .WithMessage("*invalide*");
    }

    [Fact]
    public async Task Handle_WhenTransactionFailsAfterOptimisticCacheStore_EvictsCacheEntry()
    {
        // B-H6r (cas limite) : le commit échoue APRÈS que le délégué a peuplé le cache de
        // façon optimiste. Il ne faut pas laisser un couple de jetons non persisté visible
        // à une requête concurrente qui interrogerait le cache entre-temps.
        _refreshTokens
            .Setup(r => r.TryConsumeActiveAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenConsumeResult(_userId, _tokenId, _familyId));

        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) =>
            {
                await action(ct);
                throw new InvalidOperationException("Échec simulé du commit.");
            });

        var act = async () => await CreateHandler().Handle(new RefreshTokenCommand(RawToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _rotationCache.Verify(c => c.Remove(TokenHash), Times.Once);
    }
}
