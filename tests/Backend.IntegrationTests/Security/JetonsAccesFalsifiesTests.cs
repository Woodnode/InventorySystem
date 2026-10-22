using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace InventorySystem.Backend.IntegrationTests.Security;

/// <summary>
/// Jetons d'accès forgés, expirés ou détournés : l'API doit tous les refuser (401), sans
/// expliquer pourquoi. Les jetons sont fabriqués ici avec les paramètres réels de l'API
/// (émetteur, audience, clé de test) pour ne changer qu'un élément à la fois.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class JetonsAccesFalsifiesTests
{
    private const string Issuer = "InventorySystem";
    private const string Audience = "InventorySystemClients";
    private const string RouteProtegee = "/api/v1/warehouses";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public JetonsAccesFalsifiesTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string Forger(
        string key = ApiFactory.TestJwtKey,
        string issuer = Issuer,
        string audience = Audience,
        string role = "Admin",
        DateTime? expires = null,
        DateTime? notBefore = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var expiration = expires ?? DateTime.UtcNow.AddMinutes(10);
        var token = new JwtSecurityToken(
            issuer, audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "forge@test.local"),
                new Claim(ClaimTypes.Role, role),
            ],
            notBefore: notBefore ?? expiration.AddMinutes(-20),
            expires: expiration,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Base64Url(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));

    private async Task DoitEtreRefuseAsync(string token)
    {
        var response = await _client.SendAsync(HttpMethod.Get, RouteProtegee, token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EtantDonneJetonBienForme_QuandSigneAvecLaCleDeTest_AlorsAccepte()
    {
        // Témoin : prouve que les refus ci-dessous viennent bien de l'élément modifié.
        var response = await _client.SendAsync(HttpMethod.Get, RouteProtegee, Forger());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EtantDonneJetonSigneAvecUneAutreCle_QuandAppelerLApi_AlorsRefuse()
        => await DoitEtreRefuseAsync(Forger(key: "une-autre-cle-de-signature-tout-aussi-longue-32+"));

    [Fact]
    public async Task EtantDonneJetonExpire_QuandAppelerLApi_AlorsRefuse()
        // Au-delà des 30 secondes de tolérance d'horloge accordées par l'API.
        => await DoitEtreRefuseAsync(Forger(expires: DateTime.UtcNow.AddMinutes(-2)));

    [Fact]
    public async Task EtantDonneJetonPasEncoreValide_QuandAppelerLApi_AlorsRefuse()
        => await DoitEtreRefuseAsync(Forger(
            notBefore: DateTime.UtcNow.AddMinutes(5), expires: DateTime.UtcNow.AddMinutes(20)));

    [Fact]
    public async Task EtantDonneEmetteurInconnu_QuandAppelerLApi_AlorsRefuse()
        => await DoitEtreRefuseAsync(Forger(issuer: "https://emetteur-malveillant.example"));

    [Fact]
    public async Task EtantDonneAudienceDUneAutreApplication_QuandAppelerLApi_AlorsRefuse()
        => await DoitEtreRefuseAsync(Forger(audience: "AutreApplication"));

    [Fact]
    public async Task EtantDonneJetonNonSigneAlgNone_QuandAppelerLApi_AlorsRefuse()
    {
        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");
        var exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var payload = Base64Url($$"""{"sub":"{{Guid.NewGuid()}}","http://schemas.microsoft.com/ws/2008/06/identity/claims/role":"Admin","iss":"{{Issuer}}","aud":"{{Audience}}","exp":{{exp}}}""");

        await DoitEtreRefuseAsync($"{header}.{payload}.");
    }

    [Fact]
    public async Task EtantDonneJetonEmployeDontLeRoleEstReecrit_QuandAppelerUneRouteGestionnaire_AlorsRefuse()
    {
        // Élévation de privilège : le corps du jeton est modifié, la signature d'origine conservée.
        var authentique = await RoleTokens.GetAsync(_factory, RoleTokens.Employe);
        var parties = authentique.Split('.');
        var corps = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parties[1]));
        corps.Should().Contain("\"Employe\"");
        var falsifie = $"{parties[0]}.{Base64Url(corps.Replace("\"Employe\"", "\"Admin\""))}.{parties[2]}";

        var response = await _client.SendAsync(HttpMethod.Post, "/api/v1/warehouses", falsifie,
            System.Net.Http.Json.JsonContent.Create(new { name = "Élévation", address = (string?)null }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneSignatureTronquee_QuandAppelerLApi_AlorsRefuse()
    {
        var authentique = await RoleTokens.GetAsync(_factory, RoleTokens.Admin);

        await DoitEtreRefuseAsync(authentique[..^4]);
    }

    [Fact]
    public async Task EtantDonneRefreshTokenPresenteCommeBearer_QuandAppelerLApi_AlorsRefuse()
    {
        var session = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Admin);

        await DoitEtreRefuseAsync(session.RefreshToken!);
    }

    [Fact]
    public async Task EtantDonneJetonEnQueryString_QuandAppelerUneRouteHorsHub_AlorsIgnore()
    {
        // La lecture de access_token dans l'URL est réservée au hub SignalR : ailleurs, un jeton
        // dans l'URL finirait dans les journaux et l'historique sans protéger quoi que ce soit.
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Admin);

        var response = await _client.GetAsync($"{RouteProtegee}?access_token={token}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneSchemaAutreQueBearer_QuandAppelerLApi_AlorsRefuse()
    {
        var token = await RoleTokens.GetAsync(_factory, RoleTokens.Admin);
        using var request = new HttpRequestMessage(HttpMethod.Get, RouteProtegee);
        request.Headers.TryAddWithoutValidation("Authorization", $"Basic {token}");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneJetonSansRole_QuandAppelerUneRouteEmploye_AlorsInterdit()
    {
        // Authentifié mais sans aucun des trois rôles : la politique RequireEmployeOrAbove refuse.
        var response = await _client.SendAsync(HttpMethod.Get, RouteProtegee, Forger(role: "Visiteur"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
