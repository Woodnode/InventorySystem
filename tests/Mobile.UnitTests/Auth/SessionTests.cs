using FluentAssertions;
using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Auth;
using Moq;

namespace InventoryMobile.Mobile.UnitTests.Auth;

/// <summary>
/// Session mobile : expiration anticipée de l'access token, connexion qui persiste la session,
/// restauration au démarrage, et déconnexion sans session.
/// </summary>
public sealed class SessionTests
{
    private readonly Mock<IInventoryApi> _api = new();
    private readonly Mock<ITokenStore> _store = new();

    private static AuthSession Session(DateTime expiration) => new("acces", expiration, "refresh", "e@test.local", "Émile", ["Employe"]);

    [Fact]
    public void EtantDonneJetonValideEncoreUneHeure_QuandVerifierAvecUneMinuteDeMarge_AlorsPasExpire()
        => Session(DateTime.UtcNow.AddHours(1)).IsExpiredOrExpiringSoon(TimeSpan.FromMinutes(1)).Should().BeFalse();

    [Fact]
    public void EtantDonneJetonExpirantDansTrenteSecondes_QuandVerifierAvecUneMinuteDeMarge_AlorsARafraichir()
        => Session(DateTime.UtcNow.AddSeconds(30)).IsExpiredOrExpiringSoon(TimeSpan.FromMinutes(1)).Should().BeTrue();

    [Fact]
    public void EtantDonneJetonDejaExpire_QuandVerifierSansMarge_AlorsExpire()
        => Session(DateTime.UtcNow.AddMinutes(-5)).IsExpiredOrExpiringSoon(TimeSpan.Zero).Should().BeTrue();

    [Fact]
    public async Task EtantDonneIdentifiantsValides_QuandSeConnecter_AlorsSessionPersisteeEtCourante()
    {
        var expiration = new DateTime(2026, 10, 22, 12, 15, 0, DateTimeKind.Utc);
        _api.Setup(a => a.LoginAsync(new LoginRequest("e@test.local", "Secret1A"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResultResponse("acces", expiration, "refresh", "e@test.local", "Émile", ["Employe"]));
        AuthSession? persistee = null;
        _store.Setup(s => s.SaveAsync(It.IsAny<AuthSession>())).Callback<AuthSession>(s => persistee = s).Returns(Task.CompletedTask);
        var service = new AuthService(_api.Object, _store.Object);

        var session = await service.LoginAsync("e@test.local", "Secret1A");

        session.Should().BeEquivalentTo(new { AccessToken = "acces", RefreshToken = "refresh", AccessTokenExpiresAtUtc = expiration, DisplayName = "Émile" });
        persistee.Should().Be(session);
        service.CurrentSession.Should().Be(session);
    }

    [Fact]
    public async Task EtantDonneConnexionRefusee_QuandSeConnecter_AlorsRienNEstPersiste()
    {
        _api.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("401"));
        var service = new AuthService(_api.Object, _store.Object);

        var act = () => service.LoginAsync("e@test.local", "mauvais");

        await act.Should().ThrowAsync<HttpRequestException>();
        _store.Verify(s => s.SaveAsync(It.IsAny<AuthSession>()), Times.Never);
        service.CurrentSession.Should().BeNull();
    }

    [Fact]
    public async Task EtantDonneSessionEnregistree_QuandRestaurerAuDemarrage_AlorsDevientCourante()
    {
        var enregistree = Session(DateTime.UtcNow.AddMinutes(10));
        _store.Setup(s => s.LoadAsync()).ReturnsAsync(enregistree);
        var service = new AuthService(_api.Object, _store.Object);

        var restauree = await service.RestoreSessionAsync();

        restauree.Should().Be(enregistree);
        service.CurrentSession.Should().Be(enregistree);
    }

    [Fact]
    public void EtantDonneSessionRafraichieAilleurs_QuandLAppliquer_AlorsCouranteSansRelireLeStockage()
    {
        var service = new AuthService(_api.Object, _store.Object);
        var rafraichie = Session(DateTime.UtcNow.AddMinutes(15));

        service.ApplySession(rafraichie);

        service.CurrentSession.Should().Be(rafraichie);
        _store.Verify(s => s.LoadAsync(), Times.Never);
    }

    [Fact]
    public async Task EtantDonneAucuneSession_QuandSeDeconnecter_AlorsAucunAppelServeurEtStockageVide()
    {
        _store.Setup(s => s.LoadAsync()).ReturnsAsync((AuthSession?)null);
        var service = new AuthService(_api.Object, _store.Object);

        await service.LogoutAsync();

        _api.Verify(a => a.LogoutAsync(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.ClearAsync(), Times.Once);
    }
}
