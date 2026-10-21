using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventorySystem.Backend.IntegrationTests.Auth;

/// <summary>Protections ajoutées après l'audit de sécurité, vérifiées sur l'application complète.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AuditSecurityTests
{
    /// <summary>Mot de passe des comptes de démonstration (appsettings.Development.json).</summary>
    private const string DemoPassword = "Demo@2026!";

    private readonly HttpClient _client;

    private readonly ApiFactory _factory;

    public AuditSecurityTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EtantDonneCompteDAmorcageAfficheSurLaVitrine_QuandSixMauvaisMotsDePasse_AlorsLaDemoResteAccessible()
    {
        // En production, le compte de démo affiché est celui d'AdminSeeder, pas de DemoUserSeeder.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Demo"] = "true",
                ["Seed:AdminEmail"] = ApiFactory.AdminEmail,
            })
            .Build();
        using (var scope = _factory.Services.CreateScope())
            await AdminSeeder.DisableLockoutForDemoAsync(
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), configuration, NullLogger.Instance);

        for (var i = 0; i < 6; i++)
            await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(ApiFactory.AdminEmail, "Mauvais1!"));
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(ApiFactory.AdminEmail, ApiFactory.AdminPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

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
