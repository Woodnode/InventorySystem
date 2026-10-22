using System.Net;
using System.Text;
using FluentAssertions;
using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Moq;
using Refit;

namespace InventoryMobile.Mobile.UnitTests.Scan;

/// <summary>
/// Le scan interroge /products/by-sku avec le code lu par la caméra. Ces tests passent par le
/// vrai client Refit et un serveur simulé : route et encodage du code, réponses 404/400 lues
/// comme l'écran de scan les lit, et forme JSON des mouvements envoyés à l'API.
/// </summary>
public sealed class ScanEtContratApiTests
{
    private sealed class ServeurSimule(HttpStatusCode statut, string corps, string type = "application/json") : HttpMessageHandler
    {
        public HttpRequestMessage? Requete { get; private set; }
        public string? CorpsRecu { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requete = request;
            CorpsRecu = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(statut) { Content = new StringContent(corps, Encoding.UTF8, type), RequestMessage = request };
        }
    }

    /// <summary>
    /// Client résolu par le vrai câblage de l'application (AddInventoryInfrastructure : options
    /// JSON, gestionnaire d'authentification), seul le transport réseau est remplacé.
    /// </summary>
    private static IInventoryApi Client(ServeurSimule serveur)
    {
        var tokenStore = new Mock<ITokenStore>();
        tokenStore.Setup(t => t.LoadAsync()).ReturnsAsync((AuthSession?)null);
        var services = new ServiceCollection()
            .AddSingleton(tokenStore.Object)
            .AddSingleton(Mock.Of<ISessionExpiredNotifier>())
            .AddInventoryInfrastructure("http://api.test/api/v1", Path.Combine(Path.GetTempPath(), "inutilise.db3"), "http://api.test/hub");
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = serveur));
        return services.BuildServiceProvider().GetRequiredService<IInventoryApi>();
    }

    private const string ProduitJson = """
        {"id":"8a0b3c1e-2f4d-4b6a-9c8e-1d2f3a4b5c6d","sku":"LIV-0102","name":"Le Petit Prince","description":null,
         "quantity":4,"lowStockThreshold":2,"isLowOnStock":false,"collection":"Folio","primaryLocation":"Boutique · B"}
        """;

    [Fact]
    public async Task EtantDonneCodeScanne_QuandRechercher_AlorsRouteParSkuEtFicheLue()
    {
        var serveur = new ServeurSimule(HttpStatusCode.OK, ProduitJson);

        var produit = await Client(serveur).GetProductBySkuAsync("LIV-0102");

        serveur.Requete!.Method.Should().Be(HttpMethod.Get);
        serveur.Requete.RequestUri!.AbsolutePath.Should().Be("/api/v1/products/by-sku/LIV-0102");
        produit.Should().BeEquivalentTo(new { Sku = "LIV-0102", Name = "Le Petit Prince", Quantity = 4, PrimaryLocation = "Boutique · B" });
    }

    [Theory]
    [InlineData("ABC/123", "/api/v1/products/by-sku/ABC%2F123")]
    [InlineData("A B C", "/api/v1/products/by-sku/A%20B%20C")]
    [InlineData("?admin=1", "/api/v1/products/by-sku/%3Fadmin%3D1")]
    public async Task EtantDonneCodeAvecCaracteresSpeciaux_QuandRechercher_AlorsEncodeDansUnSeulSegment(string code, string chemin)
    {
        // Un QR code peut contenir n'importe quoi : il ne doit ni changer de route ni ajouter de paramètre.
        var serveur = new ServeurSimule(HttpStatusCode.OK, ProduitJson);

        await Client(serveur).GetProductBySkuAsync(code);

        serveur.Requete!.RequestUri!.AbsolutePath.Should().Be(chemin);
        serveur.Requete.RequestUri.Query.Should().BeEmpty();
    }

    [Fact]
    public async Task EtantDonneCodeInconnu_QuandRechercher_AlorsErreurIntrouvableReconnaissable()
    {
        var serveur = new ServeurSimule(HttpStatusCode.NotFound, "", "application/problem+json");

        var act = () => Client(serveur).GetProductBySkuAsync("ZZZ-9999");

        (await act.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EtantDonneCodeHorsBornes_QuandRechercher_AlorsMotifDuServeurLisible()
    {
        var serveur = new ServeurSimule(HttpStatusCode.BadRequest,
            """{"title":"Règle métier violée","status":400,"detail":"Le SKU doit contenir entre 3 et 32 caractères."}""",
            "application/problem+json");

        var exception = (await FluentActions.Awaiting(() => Client(serveur).GetProductBySkuAsync("AB"))
            .Should().ThrowAsync<ApiException>()).Which;

        (await ApiErrorReader.ReadDetailAsync(exception)).Should().Be("Le SKU doit contenir entre 3 et 32 caractères.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>Erreur du relais</html>")]
    [InlineData("""{"title":"Sans détail"}""")]
    public async Task EtantDonneReponseDErreurSansDetail_QuandLireLeMotif_AlorsAucunMessageInvente(string corps)
    {
        var serveur = new ServeurSimule(HttpStatusCode.BadGateway, corps, "text/html");

        var exception = (await FluentActions.Awaiting(() => Client(serveur).GetProductBySkuAsync("LIV-0102"))
            .Should().ThrowAsync<ApiException>()).Which;

        (await ApiErrorReader.ReadDetailAsync(exception)).Should().BeNull();
    }

    [Fact]
    public async Task EtantDonneTransfert_QuandEnregistrer_AlorsJsonAttenduParLApi()
    {
        var serveur = new ServeurSimule(HttpStatusCode.Created, """{"id":"8a0b3c1e-2f4d-4b6a-9c8e-1d2f3a4b5c6d"}""");
        var clientGuid = Guid.Parse("11111111-2222-4333-8444-555555555555");

        await Client(serveur).RecordMovementAsync(new RecordMovementRequest(
            Guid.Empty, Guid.Empty, MobileMovementType.Transfer, 3, null, Guid.Empty, clientGuid));

        serveur.Requete!.RequestUri!.AbsolutePath.Should().Be("/api/v1/movements");
        serveur.CorpsRecu.Should().Contain("\"type\":\"Transfer\"", "l'API lit les types de mouvement en texte")
            .And.Contain("\"clientGuid\":\"11111111-2222-4333-8444-555555555555\"")
            .And.Contain("\"quantity\":3");
    }

    [Fact]
    public async Task EtantDonneRechercheVide_QuandListerLesProduits_AlorsParametreNonEnvoye()
    {
        var serveur = new ServeurSimule(HttpStatusCode.OK, """{"items":[],"totalCount":0,"page":2,"pageSize":20}""");

        await Client(serveur).GetProductsAsync(page: 2, pageSize: 20);

        var query = serveur.Requete!.RequestUri!.Query;
        query.Should().Contain("page=2").And.Contain("pageSize=20").And.NotContain("search").And.NotContain("lowStockOnly");
    }

    [Fact]
    public async Task EtantDonneConnexion_QuandEnvoyerLesIdentifiants_AlorsCorpsSansEnTeteDeTransportCookie()
    {
        // Le mobile garde son refresh token dans le corps : il ne demande jamais le cookie du web.
        var serveur = new ServeurSimule(HttpStatusCode.OK,
            """{"accessToken":"a","accessTokenExpiresAtUtc":"2026-10-22T12:00:00Z","refreshToken":"r","email":"e@test.local","displayName":"É","roles":["Employe"]}""");

        var session = await Client(serveur).LoginAsync(new LoginRequest("e@test.local", "Secret1A"));

        serveur.Requete!.Headers.Contains("X-Refresh-Token-Transport").Should().BeFalse();
        serveur.CorpsRecu.Should().Contain("\"email\":\"e@test.local\"").And.Contain("\"password\":\"Secret1A\"");
        session.RefreshToken.Should().Be("r");
    }
}
