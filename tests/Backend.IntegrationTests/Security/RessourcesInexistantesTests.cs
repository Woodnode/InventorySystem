using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace InventorySystem.Backend.IntegrationTests.Security;

/// <summary>
/// Accès à une ressource inexistante : la réponse dit « introuvable » et rien de plus. Pas de
/// trace d'appel, pas de requête SQL, pas de nom de type .NET ni de nom de table qui renseignerait
/// un attaquant sur l'implémentation.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class RessourcesInexistantesTests
{
    private static readonly string[] IndicesDImplementation =
    [
        "Exception", "   at ", "Npgsql", "EntityFramework", "SELECT ", "InventorySystem.", "stack", "postgres",
    ];

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RessourcesInexistantesTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<string> TokenAsync() => RoleTokens.GetAsync(_factory, RoleTokens.Admin);

    private static async Task<string> CorpsSansIndiceAsync(HttpResponseMessage response)
    {
        var corps = await response.Content.ReadAsStringAsync();
        foreach (var indice in IndicesDImplementation)
            corps.Should().NotContainEquivalentOf(indice);
        return corps;
    }

    [Fact]
    public async Task EtantDonneProduitInexistant_QuandLeConsulter_AlorsIntrouvableSansDetail()
    {
        var response = await _client.SendAsync(HttpMethod.Get, $"/api/v1/products/{Guid.NewGuid()}", await TokenAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await CorpsSansIndiceAsync(response);
    }

    [Fact]
    public async Task EtantDonneSkuInexistant_QuandScanner_AlorsIntrouvableSansDetail()
    {
        var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/products/by-sku/ZZZ-0000", await TokenAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await CorpsSansIndiceAsync(response);
    }

    [Fact]
    public async Task EtantDonneSkuMalForme_QuandScanner_AlorsErreurMetierLisible()
    {
        var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/products/by-sku/ab", await TokenAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CorpsSansIndiceAsync(response)).Should().Contain("entre 3 et 32 caractères");
    }

    [Fact]
    public async Task EtantDonneIdentifiantQuiNEstPasUnGuid_QuandConsulter_AlorsRouteIntrouvable()
    {
        var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/products/1%20OR%201=1", await TokenAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EtantDonneProduitInexistant_QuandLireSonStockEtSonHistorique_AlorsListesVidesIndiscernables()
    {
        // Même réponse qu'un produit réel sans stock : l'absence d'un identifiant ne se devine pas.
        var token = await TokenAsync();
        var id = Guid.NewGuid();

        var stocks = await _client.SendAsync(HttpMethod.Get, $"/api/v1/stocks/product/{id}", token);
        var historique = await _client.SendAsync(HttpMethod.Get, $"/api/v1/movements/product/{id}", token);

        stocks.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stocks.Content.ReadAsStringAsync()).Should().Be("[]");
        historique.StatusCode.Should().Be(HttpStatusCode.OK);
        (await historique.Content.ReadAsStringAsync()).Should().Contain("\"totalCount\":0");
    }

    [Theory]
    [InlineData("PUT", "/api/v1/warehouses/{0}", """{"name":"X"}""", "Entrepôt introuvable.")]
    [InlineData("PATCH", "/api/v1/warehouses/{0}/active", """{"isActive":true}""", "Entrepôt introuvable.")]
    [InlineData("PUT", "/api/v1/suppliers/{0}", """{"name":"X"}""", "Fournisseur introuvable.")]
    [InlineData("PATCH", "/api/v1/suppliers/{0}/active", """{"isActive":true}""", "Fournisseur introuvable.")]
    public async Task EtantDonneReferentielInexistant_QuandLeModifier_AlorsMessageIntrouvableSeul(
        string methode, string modele, string json, string message)
    {
        var response = await _client.SendAsync(new HttpMethod(methode), string.Format(modele, Guid.NewGuid()),
            await TokenAsync(), new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var probleme = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        probleme!.Detail.Should().Be(message);
        await CorpsSansIndiceAsync(response);
    }

    [Fact]
    public async Task EtantDonneProduitInexistant_QuandEnregistrerUnMouvement_AlorsMessageIntrouvableSansEcriture()
    {
        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/movements", await TokenAsync(),
            JsonContent.Create(new { productId = Guid.NewGuid(), warehouseId = Guid.NewGuid(), type = "In", quantity = 1 }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CorpsSansIndiceAsync(response)).Should().Contain("Produit introuvable.");
    }

    [Fact]
    public async Task EtantDonneProduitInexistant_QuandExporterSonHistorique_AlorsFichierAvecSeulementLEnTete()
    {
        var response = await _client.SendAsync(HttpMethod.Get,
            $"/api/v1/movements/export?format=Csv&productId={Guid.NewGuid()}", await TokenAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lignes = (await response.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lignes.Should().ContainSingle("seule la ligne d'en-tête est attendue");
    }

    [Fact]
    public async Task EtantDonneCourrielInconnu_QuandSeConnecter_AlorsMemeReponseQuUnMauvaisMotDePasse()
    {
        var connu = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);

        var mauvaisMotDePasse = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = connu.Email, password = "Mauvais1!" });
        var inconnu = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = $"{Guid.NewGuid():N}@test.local", password = "Mauvais1!" });

        inconnu.StatusCode.Should().Be(mauvaisMotDePasse.StatusCode);
        (await inconnu.Content.ReadAsStringAsync()).Should().Be(await mauvaisMotDePasse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EtantDonneJsonMalForme_QuandEnregistrerUnMouvement_AlorsRequeteInvalideSansTrace()
    {
        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/movements", await TokenAsync(),
            new StringContent("{\"productId\": ", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corps = await response.Content.ReadAsStringAsync();
        corps.Should().NotContain("   at ").And.NotContain("Npgsql");
    }
}
