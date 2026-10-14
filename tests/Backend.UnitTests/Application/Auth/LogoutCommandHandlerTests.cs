using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Common.Interfaces;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Auth;

public sealed class LogoutCommandHandlerTests
{
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();

    private LogoutCommandHandler CreateHandler() => new(_tokens.Object, _refreshTokens.Object);

    [Fact]
    public async Task Handle_WithToken_RevokesItsWholeFamily()
    {
        _tokens.Setup(t => t.HashRefreshToken("raw-token")).Returns("hashed-token");

        await CreateHandler().Handle(new LogoutCommand("raw-token"), CancellationToken.None);

        _refreshTokens.Verify(r => r.RevokeFamilyAsync("hashed-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithoutToken_DoesNothing(string? refreshToken)
    {
        await CreateHandler().Handle(new LogoutCommand(refreshToken), CancellationToken.None);

        _refreshTokens.Verify(r => r.RevokeFamilyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
