using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace InventorySystem.Backend.IntegrationTests.Security;

/// <summary>
/// En-têtes de sécurité (SecurityHeadersMiddleware) sur toutes les sortes de réponses, y
/// compris les erreurs, et politique CORS limitée aux origines déclarées.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class EnTetesEtCorsTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public EnTetesEtCorsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static void VerifierEnTetes(HttpResponseMessage response)
    {
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
        response.Headers.GetValues("Permissions-Policy").Single().Should().Contain("camera=()").And.Contain("geolocation=()");
        response.Headers.GetValues("Content-Security-Policy").Single()
            .Should().Contain("default-src 'none'").And.Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task EtantDonneRequeteReussie_QuandLireLesEnTetes_AlorsProtectionsPresentes()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);

        var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/warehouses", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VerifierEnTetes(response);
    }

    [Fact]
    public async Task EtantDonneRequeteNonAuthentifiee_QuandLireLesEnTetes_AlorsProtectionsPresentes()
    {
        var response = await _client.GetAsync("/api/v1/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        VerifierEnTetes(response);
    }

    [Fact]
    public async Task EtantDonneErreurDeValidation_QuandLireLesEnTetes_AlorsProtectionsPresentes()
    {
        // Réponse écrite par ExceptionHandlingMiddleware : les en-têtes posés en amont restent.
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = "pas-un-courriel", password = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        VerifierEnTetes(response);
    }

    [Fact]
    public async Task EtantDonneRouteInconnue_QuandLireLesEnTetes_AlorsProtectionsPresentes()
    {
        var response = await _client.GetAsync("/api/v1/nexiste-pas");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        VerifierEnTetes(response);
    }

    [Fact]
    public async Task EtantDonneExportDeFichier_QuandLireLesEnTetes_AlorsProtectionsEtTypeDeContenuExplicite()
    {
        // nosniff n'a d'effet que si le type est juste : un CSV ne doit pas pouvoir passer pour du HTML.
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);

        var response = await _client.SendAsync(HttpMethod.Get, "/api/v1/products/export?format=Csv", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VerifierEnTetes(response);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
    }

    [Fact]
    public async Task EtantDonneOrigineDeclaree_QuandPreflightCors_AlorsAutoriseeAvecIdentifiants()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be("http://localhost:5173");
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle().Which.Should().Be("true");
    }

    [Fact]
    public async Task EtantDonneOrigineInconnue_QuandPreflightCors_AlorsAucuneAutorisation()
    {
        // Avec AllowCredentials, refléter n'importe quelle origine livrerait la session à tout site.
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "https://site-malveillant.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    [Fact]
    public async Task EtantDonneOrigineInconnue_QuandRequeteSimple_AlorsReponseSansEnTeteCors()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://site-malveillant.example");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task EtantDonneOrigineDeclaree_QuandExport_AlorsEnTeteDeTroncatureLisibleParLeFront()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products/export?format=Csv");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Expose-Headers").Single().Should().Contain("X-Export-Truncated");
    }
}
