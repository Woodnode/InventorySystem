using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySystem.Backend.IntegrationTests.Security;

/// <summary>
/// Matrice des autorisations : chaque route métier de l'API est appelée sans jeton, puis avec
/// un jeton Employe, Gestionnaire et Admin. Les corps envoyés sont volontairement invalides
/// (identifiants inconnus, champs vides) : un rôle autorisé reçoit une erreur de validation,
/// jamais un 401/403, et aucun test ne modifie les données partagées.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AutorisationParRoleTests
{
    /// <summary>Rôle minimal exigé par une route.</summary>
    public enum Niveau { Employe, Gestionnaire }

    public sealed record Route(string Methode, string Modele, string Url, Niveau Niveau, Func<HttpContent?> Corps)
    {
        public override string ToString() => $"{Methode} {Url}";
    }

    private static readonly string Inconnu = "3f1c2a9e-0000-4000-8000-000000000001";

    private static HttpContent Json(object corps) => JsonContent.Create(corps);

    private static readonly IReadOnlyList<Route> RoutesMetier =
    [
        new("GET", "api/v1/Dashboard/stats", "/api/v1/dashboard/stats", Niveau.Employe, () => null),

        new("POST", "api/v1/Movements", "/api/v1/movements", Niveau.Employe, () => Json(new { })),
        new("GET", "api/v1/Movements/recent", "/api/v1/movements/recent", Niveau.Employe, () => null),
        new("GET", "api/v1/Movements/product/{productId:guid}", $"/api/v1/movements/product/{Inconnu}", Niveau.Employe, () => null),
        new("GET", "api/v1/Movements/export", $"/api/v1/movements/export?format=Csv&productId={Inconnu}", Niveau.Employe, () => null),

        new("POST", "api/v1/Products", "/api/v1/products", Niveau.Gestionnaire, () => Json(new { })),
        new("GET", "api/v1/Products/collections", "/api/v1/products/collections", Niveau.Employe, () => null),
        new("GET", "api/v1/Products", "/api/v1/products?pageSize=1", Niveau.Employe, () => null),
        new("GET", "api/v1/Products/{id:guid}", $"/api/v1/products/{Inconnu}", Niveau.Employe, () => null),
        new("GET", "api/v1/Products/by-sku/{sku}", "/api/v1/products/by-sku/ZZZ-INCONNU", Niveau.Employe, () => null),
        new("GET", "api/v1/Products/low-stock", "/api/v1/products/low-stock?pageSize=1", Niveau.Employe, () => null),
        new("GET", "api/v1/Products/export", "/api/v1/products/export?format=Csv", Niveau.Employe, () => null),
        new("POST", "api/v1/Products/import", "/api/v1/products/import", Niveau.Gestionnaire, () => new MultipartFormDataContent()),

        new("GET", "api/v1/Stocks/product/{productId:guid}", $"/api/v1/stocks/product/{Inconnu}", Niveau.Employe, () => null),

        new("POST", "api/v1/Suppliers", "/api/v1/suppliers", Niveau.Gestionnaire, () => Json(new { })),
        new("GET", "api/v1/Suppliers", "/api/v1/suppliers", Niveau.Employe, () => null),
        new("PUT", "api/v1/Suppliers/{id:guid}", $"/api/v1/suppliers/{Inconnu}", Niveau.Gestionnaire, () => Json(new { name = "Inconnu" })),
        new("PATCH", "api/v1/Suppliers/{id:guid}/active", $"/api/v1/suppliers/{Inconnu}/active", Niveau.Gestionnaire, () => Json(new { isActive = false })),

        new("POST", "api/v1/Warehouses", "/api/v1/warehouses", Niveau.Gestionnaire, () => Json(new { })),
        new("GET", "api/v1/Warehouses", "/api/v1/warehouses", Niveau.Employe, () => null),
        new("PUT", "api/v1/Warehouses/{id:guid}", $"/api/v1/warehouses/{Inconnu}", Niveau.Gestionnaire, () => Json(new { name = "Inconnu" })),
        new("PATCH", "api/v1/Warehouses/{id:guid}/active", $"/api/v1/warehouses/{Inconnu}/active", Niveau.Gestionnaire, () => Json(new { isActive = false })),
    ];

    /// <summary>Routes d'authentification : ouvertes à tous, protégées par la limite de débit.</summary>
    private static readonly IReadOnlyList<(string Methode, string Modele)> RoutesAnonymes =
    [
        ("POST", "api/v1/Auth/register"),
        ("POST", "api/v1/Auth/login"),
        ("POST", "api/v1/Auth/refresh"),
        ("POST", "api/v1/Auth/logout"),
    ];

    // Clés textuelles (et non les objets Route) : xUnit sait alors énumérer chaque cas à part.
    public static TheoryData<string> Routes() => new(RoutesMetier.Select(r => r.ToString()));

    public static TheoryData<string> RoutesGestionnaire()
        => new(RoutesMetier.Where(r => r.Niveau == Niveau.Gestionnaire).Select(r => r.ToString()));

    private static Route Trouver(string cle) => RoutesMetier.Single(r => r.ToString() == cle);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AutorisationParRoleTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<HttpResponseMessage> AppelerAsync(Route route, string? token)
        => await _client.SendAsync(new HttpMethod(route.Methode), route.Url, token, route.Corps());

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EtantDonneAucunJeton_QuandAppelerRouteMetier_AlorsNonAutoriseSansCorps(string cle)
    {
        var route = Trouver(cle);
        var response = await AppelerAsync(route, token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle(h => h.Scheme == "Bearer");
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty("un refus ne doit rien dire de la ressource");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EtantDonneJetonEmploye_QuandAppelerRouteMetier_AlorsAccesSelonNiveau(string cle)
    {
        var route = Trouver(cle);
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);

        var response = await AppelerAsync(route, token);

        if (route.Niveau == Niveau.Gestionnaire)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        }
        else
        {
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EtantDonneJetonGestionnaire_QuandAppelerRouteMetier_AlorsAccesAccorde(string cle)
    {
        var route = Trouver(cle);
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);

        var response = await AppelerAsync(route, token);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EtantDonneJetonAdmin_QuandAppelerRouteMetier_AlorsAccesAccorde(string cle)
    {
        var route = Trouver(cle);
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Admin);

        var response = await AppelerAsync(route, token);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(RoutesGestionnaire))]
    public async Task EtantDonneJetonEmploye_QuandEcrireAvecUnCorpsValide_AlorsRefuseAvantToutTraitement(string cle)
    {
        var route = Trouver(cle);
        // Le refus vient de la politique, pas de la validation : même un corps qui serait
        // accepté par un Gestionnaire reçoit 403, et rien n'est créé.
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);
        var nom = $"Refuse-{Guid.NewGuid():N}"[..20];
        HttpContent? corps = route.Methode == "POST" && route.Modele is "api/v1/Warehouses" or "api/v1/Suppliers"
            ? Json(new { name = nom })
            : route.Corps();

        var response = await _client.SendAsync(new HttpMethod(route.Methode), route.Url, token, corps);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var admin = await RoleTokens.GetAsync(_factory, RoleTokens.Admin);
        var entrepots = await (await _client.SendAsync(HttpMethod.Get, "/api/v1/warehouses", admin)).Content.ReadAsStringAsync();
        var fournisseurs = await (await _client.SendAsync(HttpMethod.Get, "/api/v1/suppliers", admin)).Content.ReadAsStringAsync();
        entrepots.Should().NotContain(nom);
        fournisseurs.Should().NotContain(nom);
    }

    [Fact]
    public void EtantDonneToutesLesRoutesDesControleurs_QuandComparerALaMatrice_AlorsAucuneRouteOubliee()
    {
        // Une route ajoutée sans être ajoutée ici ferait échouer ce test : la matrice ne
        // peut pas laisser passer une route nouvelle, ni son niveau d'accès, sans relecture.
        var sources = _factory.Services.GetRequiredService<EndpointDataSource>();
        var routesExposees = sources.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(m => $"{m} {e.RoutePattern.RawText}".ToLowerInvariant()))
            .ToHashSet();

        var routesTestees = RoutesMetier.Select(r => $"{r.Methode} {r.Modele}")
            .Concat(RoutesAnonymes.Select(r => $"{r.Methode} {r.Modele}"))
            .Select(r => r.ToLowerInvariant())
            .ToHashSet();

        routesExposees.Should().BeEquivalentTo(routesTestees);
    }

    [Fact]
    public async Task EtantDonneEmployeConnecte_QuandCreerUnCompteAdmin_AlorsRefuse()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);
        var email = $"{Guid.NewGuid():N}@test.local";

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/auth/register", token,
            Json(new { email, password = "Test1234!", displayName = "Promu", role = "Admin" }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Test1234!" });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "le compte ne doit pas avoir été créé");
    }

    [Fact]
    public async Task EtantDonneGestionnaireConnecte_QuandCreerUnCompteGestionnaire_AlorsRefuse()
    {
        // Seul un Admin peut attribuer un rôle autre qu'Employe.
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Gestionnaire);

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/auth/register", token,
            Json(new { email = $"{Guid.NewGuid():N}@test.local", password = "Test1234!", displayName = "Promu", role = "Gestionnaire" }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EtantDonneAdminConnecte_QuandCreerUnCompteGestionnaire_AlorsCompteCreeAvecCeRole()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Admin);
        var email = $"{Guid.NewGuid():N}@test.local";

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/auth/register", token,
            Json(new { email, password = "Test1234!", displayName = "Nouveau gestionnaire", role = "Gestionnaire" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Test1234!" });
        var session = await login.Content.ReadFromJsonAsync<InventorySystem.Application.Auth.Dtos.AuthResultDto>();
        session!.Roles.Should().ContainSingle().Which.Should().Be("Gestionnaire");
    }

    [Fact]
    public async Task EtantDonneAdminConnecte_QuandCreerUnCompte_AlorsSaPropreSessionNEstPasRemplacee()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Admin);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = Json(new { email = $"{Guid.NewGuid():N}@test.local", password = "Test1234!", displayName = "Employé", role = "Employe" }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Refresh-Token-Transport", "cookie");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeFalse("le cookie de l'Admin ne doit pas être écrasé par celui du nouveau compte");
    }
}
