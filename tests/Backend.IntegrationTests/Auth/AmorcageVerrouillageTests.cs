using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventorySystem.Backend.IntegrationTests.Auth;

/// <summary>
/// Un compte de démonstration trouvé verrouillé au démarrage est entièrement déverrouillé :
/// verrouillage coupé ET date de fin effacée, pas seulement le premier des deux.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AmorcageVerrouillageTests
{
    /// <summary>Mot de passe des comptes de démonstration (appsettings.Development.json).</summary>
    private const string DemoPassword = "Demo@2026!";

    private readonly ApiFactory _factory;

    public AmorcageVerrouillageTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EtantDonneCompteDeDemoVerrouille_QuandRelancerLAmorcage_AlorsVerrouillageEtDateDeFinEffaces()
    {
        const string email = "gestionnaire@inventaire.invalid";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1));

        await DemoUserSeeder.SeedAsync(users, scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>(),
            scope.ServiceProvider.GetRequiredService<IConfiguration>(), NullLogger.Instance);

        var relu = (await users.FindByEmailAsync(email))!;
        relu.LockoutEnabled.Should().BeFalse();
        relu.LockoutEnd.Should().BeNull();
        var connexion = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = DemoPassword });
        connexion.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EtantDonneAdminDAmorcageVerrouilleSurLaVitrine_QuandDesactiverLeVerrouillage_AlorsDateDeFinEtEchecsEffaces()
    {
        var session = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Admin);
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(session.Email))!;
        await users.AccessFailedAsync(user);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Demo"] = "true",
                ["Seed:AdminEmail"] = session.Email,
            })
            .Build();

        await AdminSeeder.DisableLockoutForDemoAsync(users, configuration, NullLogger.Instance);

        var relu = (await users.FindByEmailAsync(session.Email))!;
        relu.LockoutEnabled.Should().BeFalse();
        relu.LockoutEnd.Should().BeNull();
        relu.AccessFailedCount.Should().Be(0);
    }
}
