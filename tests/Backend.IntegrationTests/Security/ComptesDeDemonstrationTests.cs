using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventorySystem.Backend.IntegrationTests.Security;

/// <summary>
/// Les comptes de démonstration ont des identifiants publics. Un visiteur ne doit pouvoir ni
/// les verrouiller (mauvais mots de passe en série), ni les détourner (réinscription, mot de
/// passe écrasé au redémarrage). Un compte ordinaire, lui, garde le verrouillage.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ComptesDeDemonstrationTests
{
    /// <summary>Mot de passe des comptes de démonstration (appsettings.Development.json).</summary>
    private const string DemoPassword = "Demo@2026!";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ComptesDeDemonstrationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public static TheoryData<string> Comptes() => new(DemoUserSeeder.Emails);

    private Task<HttpResponseMessage> ConnexionAsync(string email, string password)
        => _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    [Theory]
    [MemberData(nameof(Comptes))]
    public async Task EtantDonneCompteDeDemo_QuandLireSaConfiguration_AlorsVerrouillageDesactive(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(email);

        user.Should().NotBeNull();
        user!.LockoutEnabled.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Comptes))]
    public async Task EtantDonneCompteDeDemo_QuandDixMauvaisMotsDePasse_AlorsConnexionToujoursPossible(string email)
    {
        for (var i = 0; i < 10; i++)
            (await ConnexionAsync(email, "Mauvais1!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var response = await ConnexionAsync(email, DemoPassword);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("employe@inventaire.invalid", "Employe")]
    [InlineData("gestionnaire@inventaire.invalid", "Gestionnaire")]
    [InlineData("admin@inventaire.invalid", "Admin")]
    public async Task EtantDonneCompteDeDemo_QuandSeConnecter_AlorsRoleAttendu(string email, string role)
    {
        var response = await ConnexionAsync(email, DemoPassword);

        var session = await response.Content.ReadFromJsonAsync<InventorySystem.Application.Auth.Dtos.AuthResultDto>();
        session!.Roles.Should().ContainSingle().Which.Should().Be(role);
    }

    [Fact]
    public async Task EtantDonneCompteOrdinaire_QuandCinqMauvaisMotsDePasse_AlorsVerrouilleAvecLeMemeMessage()
    {
        // Contraste : hors démonstration, le verrouillage protège le compte. Le message reste
        // celui d'un mauvais mot de passe, pour ne pas confirmer que le compte existe.
        var session = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);
        var mauvais = await ConnexionAsync(session.Email, "Mauvais1!");
        var messageMauvais = await mauvais.Content.ReadAsStringAsync();
        for (var i = 0; i < 4; i++)
            await ConnexionAsync(session.Email, "Mauvais1!");

        var response = await ConnexionAsync(session.Email, RoleTokens.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Be(messageMauvais);
    }

    [Fact]
    public async Task EtantDonneCompteDeDemo_QuandRelancerLAmorcageAvecUnAutreMotDePasse_AlorsMotDePasseConserve()
    {
        // L'amorçage ne réécrit jamais un compte existant : changer Seed:DemoPassword ne doit pas
        // ouvrir un compte qui serait devenu réel.
        using var scope = _factory.Services.CreateScope();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = "Autre@2027!" })
            .Build();

        await DemoUserSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>(), configuration, NullLogger.Instance);

        (await ConnexionAsync("employe@inventaire.invalid", DemoPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConnexionAsync("employe@inventaire.invalid", "Autre@2027!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Comptes))]
    public async Task EtantDonneCompteDeDemo_QuandSeReinscrireAvecSonAdresse_AlorsRefuseEtMotDePasseInchange(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "Pirate1234", displayName = "Pirate", role = "Employe" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ConnexionAsync(email, "Pirate1234")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ConnexionAsync(email, DemoPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EtantDonneAucunMotDePasseDeDemoConfigure_QuandAmorcer_AlorsAucunCompteCree()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var avant = users.Users.Count();

        await DemoUserSeeder.SeedAsync(users, scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>(),
            new ConfigurationBuilder().Build(), NullLogger.Instance);

        users.Users.Count().Should().Be(avant);
    }

    [Fact]
    public void EtantDonneAdressesDeDemo_QuandLesLire_AlorsDomaineReserveQuiNePeutPasRecevoirDeCourriel()
    {
        DemoUserSeeder.Emails.Should().HaveCount(3).And.OnlyContain(e => e.EndsWith("@inventaire.invalid"));
    }

    [Fact]
    public async Task EtantDonneVitrineSansDemo_QuandDesactiverLeVerrouillage_AlorsCompteDAmorcageInchange()
    {
        // Hors site vitrine, l'Admin d'amorçage garde le verrouillage : son mot de passe n'est pas public.
        var session = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Admin);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Demo"] = "false",
                ["Seed:AdminEmail"] = session.Email,
            })
            .Build();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await AdminSeeder.DisableLockoutForDemoAsync(users, configuration, NullLogger.Instance);

        (await users.FindByEmailAsync(session.Email))!.LockoutEnabled.Should().BeTrue();
    }
}
