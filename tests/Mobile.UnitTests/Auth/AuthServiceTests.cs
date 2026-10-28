using FluentAssertions;
using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Auth;
using Moq;
using Xunit;

namespace InventoryMobile.Mobile.UnitTests.Auth;

/// <summary>
/// Non-régression de l'audit de sécurité : la déconnexion mobile révoque le refresh token
/// côté serveur, sans jamais empêcher la déconnexion locale si le réseau échoue.
/// </summary>
public sealed class AuthServiceTests
{
    private readonly Mock<IInventoryApi> _api = new();
    private readonly Mock<ITokenStore> _tokenStore = new();

    private static AuthSession Session(string refreshToken = "refresh-1")
        => new("access-1", DateTime.UtcNow.AddMinutes(15), refreshToken, "user@test.local", "Jean Dupont", new[] { "Employe" });

    [Fact]
    public async Task LogoutAsync_WithStoredSession_RevokesRefreshTokenOnServerThenClearsStore()
    {
        _tokenStore.Setup(t => t.LoadAsync()).ReturnsAsync(Session("refresh-a-revoquer"));
        var sut = new AuthService(_api.Object, _tokenStore.Object);

        await sut.LogoutAsync();

        _api.Verify(a => a.LogoutAsync(
            It.Is<RefreshTokenRequest>(r => r.RefreshToken == "refresh-a-revoquer"),
            It.IsAny<CancellationToken>()), Times.Once);
        _tokenStore.Verify(t => t.ClearAsync(), Times.Once);
        sut.CurrentSession.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_WhenServerUnreachable_StillClearsLocalSession()
    {
        _tokenStore.Setup(t => t.LoadAsync()).ReturnsAsync(Session());
        _api
            .Setup(a => a.LogoutAsync(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Réseau indisponible"));
        var sut = new AuthService(_api.Object, _tokenStore.Object);

        await sut.LogoutAsync();

        _tokenStore.Verify(t => t.ClearAsync(), Times.Once);
        sut.CurrentSession.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_WithoutSession_DoesNotCallServer()
    {
        _tokenStore.Setup(t => t.LoadAsync()).ReturnsAsync((AuthSession?)null);
        var sut = new AuthService(_api.Object, _tokenStore.Object);

        await sut.LogoutAsync();

        _api.Verify(a => a.LogoutAsync(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenStore.Verify(t => t.ClearAsync(), Times.Once);
    }
}
