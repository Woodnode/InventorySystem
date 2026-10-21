using FluentAssertions;
using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Auth;
using Moq;
using Xunit;

namespace InventoryMobile.Mobile.UnitTests.Auth;

/// <summary>
/// La déconnexion effaçait seulement le SecureStorage : le refresh token restait valable 7 jours
/// côté serveur, et une copie fuitée survivait à la déconnexion.
/// </summary>
public sealed class AuthServiceLogoutTests
{
    private readonly Mock<IInventoryApi> _api = new();
    private readonly Mock<ITokenStore> _store = new();

    private static AuthSession Session() =>
        new("access-1", DateTime.UtcNow.AddMinutes(15), "refresh-1", "user@test.local", "Jean Dupont", new[] { "Employe" });

    [Fact]
    public async Task EtantDonneSessionOuverte_QuandDeconnexion_AlorsServeurPrevenuAvecLeRefreshToken()
    {
        var service = new AuthService(_api.Object, _store.Object);
        service.ApplySession(Session());

        await service.LogoutAsync();

        _api.Verify(a => a.LogoutAsync(It.Is<RefreshTokenRequest>(r => r.RefreshToken == "refresh-1"), It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.ClearAsync(), Times.Once);
        service.CurrentSession.Should().BeNull();
    }

    [Fact]
    public async Task EtantDonneHorsLigne_QuandDeconnexion_AlorsSessionLocaleEffaceeQuandMeme()
    {
        _api.Setup(a => a.LogoutAsync(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("réseau coupé"));
        var service = new AuthService(_api.Object, _store.Object);
        service.ApplySession(Session());

        await service.LogoutAsync();

        _store.Verify(s => s.ClearAsync(), Times.Once);
        service.CurrentSession.Should().BeNull();
    }
}
