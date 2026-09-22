using FluentAssertions;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Application.Common.Interfaces;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Auth;

/// <summary>
/// Couvre le mapping identifiants invalides -> <see cref="AuthenticationException"/>
/// (401 côté middleware — voir AUDIT.md B-R2), en isolation.
/// </summary>
public sealed class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private LoginCommandHandler CreateHandler()
        => new(_identity.Object, _tokens.Object, _refreshTokens.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_WithWrongPasswordOrUnknownEmail_ThrowsAuthenticationException()
    {
        // ValidateCredentialsAsync renvoie null aussi bien pour un email inconnu qu'un mauvais
        // mot de passe (message générique volontaire — voir plan §6, pas d'énumération de comptes).
        _identity
            .Setup(i => i.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityUserInfo?)null);

        var act = async () => await CreateHandler().Handle(
            new LoginCommand("user@test.local", "wrong"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationException>();
        _tokens.Verify(t => t.GenerateAccessToken(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_IssuesTokens()
    {
        var userId = Guid.NewGuid();
        var user = new IdentityUserInfo(userId, "user@test.local", "Jean Dupont", new[] { "Employe" });
        _identity
            .Setup(i => i.ValidateCredentialsAsync("user@test.local", "Test1234!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns("raw-refresh-token");
        _tokens.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns("hashed-refresh-token");
        _tokens.Setup(t => t.GenerateAccessToken(userId, user.Email, user.Roles))
            .Returns(new GeneratedAccessToken("access-token", DateTime.UtcNow.AddMinutes(15)));
        _tokens.Setup(t => t.GetRefreshTokenExpiryUtc()).Returns(DateTime.UtcNow.AddDays(7));

        var result = await CreateHandler().Handle(
            new LoginCommand("user@test.local", "Test1234!"), CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        result.Email.Should().Be("user@test.local");
    }
}
