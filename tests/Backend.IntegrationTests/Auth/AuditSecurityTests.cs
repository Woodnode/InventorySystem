using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Auth.Commands;

namespace InventorySystem.Backend.IntegrationTests.Auth;

/// <summary>Protections ajoutées après l'audit de sécurité, vérifiées sur l'application complète.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AuditSecurityTests
{
    /// <summary>Mot de passe des comptes de démonstration (appsettings.Development.json).</summary>
    private const string DemoPassword = "Demo@2026!";

    private readonly HttpClient _client;

    public AuditSecurityTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task EtantDonneCompteDeDemo_QuandSixMauvaisMotsDePasse_AlorsLaDemoResteAccessible()
    {
        const string email = "employe@inventaire.invalid";
        for (var i = 0; i < 6; i++)
            await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, "Mauvais1!"));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, DemoPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EtantDonneReponseDeLApi_QuandLireLesEnTetes_AlorsProtectionsPresentes()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand("inconnu@test.local", "Test1234!"));

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("frame-ancestors 'none'");
    }
}
