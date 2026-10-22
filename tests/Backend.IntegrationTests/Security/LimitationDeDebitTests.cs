using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySystem.Backend.IntegrationTests.Security;

/// <summary>
/// Application dont la limite d'authentification est abaissée à 3 requêtes par minute, et
/// dont l'adresse du « client TCP » se choisit par l'en-tête de test <see cref="RemoteIpHeader"/>
/// (le serveur de test n'en fournit aucune). Conteneur PostgreSQL propre : la limite basse
/// gênerait les autres classes de tests.
/// </summary>
public sealed class LimitedApiFactory : ApiFactory
{
    public const int Limite = 3;
    public const string RemoteIpHeader = "X-Test-Remote-Ip";

    protected override IDictionary<string, string?> AdditionalSettings { get; } = new Dictionary<string, string?>
    {
        ["RateLimiting:AuthPerMinute"] = Limite.ToString(),
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, RemoteIpStartupFilter>());
    }

    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // Placé avant tout le pipeline de l'API, donc avant UseForwardedHeaders.
            app.Use((context, suivant) =>
            {
                if (context.Request.Headers.TryGetValue(RemoteIpHeader, out var ip))
                    context.Connection.RemoteIpAddress = IPAddress.Parse(ip!);
                return suivant(context);
            });
            next(app);
        };
    }
}

/// <summary>
/// Limite de débit sur l'authentification : seule défense contre la force brute sur les
/// comptes de démonstration, qui ne se verrouillent pas. Chaque test prend ses propres
/// adresses IP, donc sa propre fenêtre : l'ordre d'exécution n'a pas d'effet.
/// </summary>
public sealed class LimitationDeDebitTests : IClassFixture<LimitedApiFactory>
{
    private readonly HttpClient _client;

    public LimitationDeDebitTests(LimitedApiFactory factory) => _client = factory.CreateClient();

    private async Task<HttpResponseMessage> ConnexionAsync(string remoteIp, string? clientIpTransmise = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "inconnu@test.local", password = "Mauvais1!" }),
        };
        request.Headers.Add(LimitedApiFactory.RemoteIpHeader, remoteIp);
        if (clientIpTransmise is not null)
            request.Headers.Add("X-Real-Client-IP", clientIpTransmise);
        return await _client.SendAsync(request);
    }

    private async Task EpuiserAsync(string remoteIp, string? clientIpTransmise = null)
    {
        for (var i = 0; i < LimitedApiFactory.Limite; i++)
            (await ConnexionAsync(remoteIp, clientIpTransmise)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneTroisEchecsDeConnexion_QuandQuatriemeEssai_AlorsTropDeRequetes()
    {
        await EpuiserAsync("203.0.113.1");

        var response = await ConnexionAsync("203.0.113.1");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty("le refus ne renseigne ni la limite ni la fenêtre");
    }

    [Fact]
    public async Task EtantDonneLimiteAtteinte_QuandBonMotDePasse_AlorsToujoursRefuse()
    {
        // Sinon un attaquant n'aurait qu'à essayer le bon mot de passe pour savoir qu'il l'a trouvé.
        await EpuiserAsync("203.0.113.2");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword }),
        };
        request.Headers.Add(LimitedApiFactory.RemoteIpHeader, "203.0.113.2");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task EtantDonneLimiteAtteinteSurLaConnexion_QuandInscriptionOuRafraichissement_AlorsAussiBloques()
    {
        // Une seule enveloppe pour toutes les routes d'authentification d'une même adresse.
        await EpuiserAsync("203.0.113.3");

        foreach (var (url, corps) in new (string, object)[]
        {
            ("/api/v1/auth/refresh", new { refreshToken = "x" }),
            ("/api/v1/auth/register", new { email = "a@test.local", password = "Test1234!", displayName = "A", role = "Employe" }),
            ("/api/v1/auth/logout", new { refreshToken = "x" }),
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corps) };
            request.Headers.Add(LimitedApiFactory.RemoteIpHeader, "203.0.113.3");
            (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests, url);
        }
    }

    [Fact]
    public async Task EtantDonneUneAdresseBloquee_QuandUneAutreAdresseSeConnecte_AlorsElleGardeSonBudget()
    {
        await EpuiserAsync("203.0.113.4");

        var response = await ConnexionAsync("203.0.113.5");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneClientPublicQuiFalsifieLEnTeteDuRelais_QuandDepasserLaLimite_AlorsToujoursBloque()
    {
        // L'en-tête du relais n'est cru que s'il arrive d'une adresse privée (Caddy, Docker).
        await EpuiserAsync("203.0.113.6");

        var response = await ConnexionAsync("203.0.113.6", clientIpTransmise: "198.51.100.66");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task EtantDonneRelaisPrive_QuandDeuxVisiteursPassentParLui_AlorsChacunSaPropreLimite()
    {
        // Sans l'en-tête du relais, tous les visiteurs partageraient l'adresse de Caddy.
        await EpuiserAsync("10.0.0.5", clientIpTransmise: "198.51.100.10");

        (await ConnexionAsync("10.0.0.5", clientIpTransmise: "198.51.100.10")).StatusCode
            .Should().Be(HttpStatusCode.TooManyRequests);
        (await ConnexionAsync("10.0.0.5", clientIpTransmise: "198.51.100.11")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneDeuxAdressesIpv6DuMemeSlash64_QuandSeConnecter_AlorsBudgetPartage()
    {
        await EpuiserAsync("2001:db8:aaaa:1::1");

        var response = await ConnexionAsync("2001:db8:aaaa:1:ffff::2");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task EtantDonneAdresseBloqueeSurLAuthentification_QuandAppelerUneRouteHorsAuthentification_AlorsPasDeLimite()
    {
        await EpuiserAsync("203.0.113.7");

        for (var i = 0; i < LimitedApiFactory.Limite * 2; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.Add(LimitedApiFactory.RemoteIpHeader, "203.0.113.7");
            (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task EtantDonneReponseTropDeRequetes_QuandLireLesEnTetes_AlorsProtectionsPresentes()
    {
        await EpuiserAsync("203.0.113.8");

        var response = await ConnexionAsync("203.0.113.8");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
    }
}
