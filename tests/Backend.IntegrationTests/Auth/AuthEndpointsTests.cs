using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;

namespace InventorySystem.Backend.IntegrationTests.Auth;

/// <summary>
/// Scénarios de bout en bout pour Register/Login/Refresh, exécutés contre l'application
/// complète (vraie base PostgreSQL éphémère, vrai pipeline JWT) — voir plan §10.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AuthEndpointsTests
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(ApiFactory factory) => _client = factory.CreateClient();

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    private Task<HttpResponseMessage> RegisterAsync(string email, string role = "Employe", string password = "Test1234!")
        => _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterCommand(email, password, "Jean Dupont", role));

    // --- Register ---

    [Fact]
    public async Task Register_WithValidData_ReturnsAccessAndRefreshTokens()
    {
        var response = await RegisterAsync(UniqueEmail());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Roles.Should().ContainSingle().Which.Should().Be("Employe");
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        var response = await RegisterAsync(UniqueEmail(), password: "weak");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await RegisterAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_AsAdminWithoutBeingAuthenticated_IsForbidden()
    {
        // Seul un Admin déjà connecté peut créer un compte Admin/Gestionnaire (voir AuthController).
        var response = await RegisterAsync(UniqueEmail(), role: "Admin");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Login ---

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, "Test1234!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        result!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, "MauvaisMotDePasse1"));

        // AuthenticationException -> 401, pas 400 : ce n'est pas une erreur de validation
        // de la requête, c'est un échec d'authentification (voir B-R2).
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsSameErrorAsWrongPassword()
    {
        // Le message doit être générique : ne jamais révéler si c'est l'email ou le
        // mot de passe qui est incorrect (évite l'énumération de comptes — voir plan §6).
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginCommand(UniqueEmail(), "Test1234!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- Refresh ---

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokenPair()
    {
        var email = UniqueEmail();
        var registerResponse = await RegisterAsync(email);
        var original = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshTokenCommand(original!.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewed = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        renewed!.AccessToken.Should().NotBe(original.AccessToken);
        renewed.RefreshToken.Should().NotBe(original.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ImmediateReplayOfConsumedToken_ReturnsSameTokenPairFromCache()
    {
        // Fenêtre de grâce de 30s (voir MemoryRefreshRotationCache / B-H6r2) : un rejeu immédiat
        // du même token juste consommé est traité comme un perdant de course concurrente, pas
        // comme un vol — il reçoit le même couple de jetons que la première requête (200, pas
        // une erreur). La vraie détection de réutilisation malveillante (hors fenêtre) est
        // couverte par RefreshTokenCommandHandlerTests côté Backend.UnitTests, qui mocke
        // IRefreshTokenRepository.TryHandleReuseAsync sans dépendre d'un vrai délai de 30s.
        var email = UniqueEmail();
        var registerResponse = await RegisterAsync(email);
        var original = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>();

        var first = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshTokenCommand(original!.RefreshToken));
        var replay = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshTokenCommand(original.RefreshToken));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstResult = await first.Content.ReadFromJsonAsync<AuthResultDto>();
        var replayResult = await replay.Content.ReadFromJsonAsync<AuthResultDto>();
        replayResult!.AccessToken.Should().Be(firstResult!.AccessToken);
        replayResult.RefreshToken.Should().Be(firstResult.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithGarbageToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshTokenCommand("ceci-n-est-pas-un-refresh-token-valide"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- Policies de rôles sur les endpoints métier (vérifie que la sécurité tient vraiment) ---

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/products/low-stock");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithEmployeToken_CanRead()
    {
        var email = UniqueEmail();
        var registerResponse = await RegisterAsync(email);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products/low-stock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProduct_WithEmployeToken_IsForbidden()
    {
        // Employé peut consulter mais pas créer — réservé à Gestionnaire/Admin (voir ProductsController).
        var email = UniqueEmail();
        var registerResponse = await RegisterAsync(email);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent.Create(new
            {
                sku = "TST-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                name = "Produit de test",
                description = (string?)null,
                lowStockThreshold = 2,
                warehouseId = Guid.NewGuid(),
                initialQuantity = 10,
                supplierId = (Guid?)null,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
