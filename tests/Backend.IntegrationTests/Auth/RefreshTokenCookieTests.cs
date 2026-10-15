using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace InventorySystem.Backend.IntegrationTests.Auth;

/// <summary>
/// Transport du refresh token : cookie httpOnly pour le client web (qui le demande par
/// l'en-tête X-Refresh-Token-Transport), corps JSON pour le client mobile. Le client HTTP
/// ne gère pas les cookies lui-même : chaque test lit Set-Cookie et renvoie Cookie à la main,
/// pour vérifier exactement ce que le navigateur recevrait.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class RefreshTokenCookieTests
{
    private const string CookieName = "inventory_refresh";
    private const string TransportHeader = "X-Refresh-Token-Transport";

    private readonly HttpClient _client;

    public RefreshTokenCookieTests(ApiFactory factory)
        => _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    private async Task<string> RegisterAsync()
    {
        var email = UniqueEmail();
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register", new RegisterCommand(email, "Test1234!", "Jean Dupont", "Employe"));
        response.EnsureSuccessStatusCode();
        return email;
    }

    private async Task<HttpResponseMessage> WebLoginAsync(string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginCommand(email, "Test1234!")),
        };
        request.Headers.Add(TransportHeader, "cookie");
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostWithCookieAsync(string path, string cookieValue, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(HeaderNames.Cookie, $"{CookieName}={cookieValue}");
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return _client.SendAsync(request);
    }

    private static SetCookieHeaderValue? RefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
            return null;

        return SetCookieHeaderValue.ParseList(values.ToList())
            .SingleOrDefault(c => c.Name.Value == CookieName);
    }

    private static async Task<JsonElement> JsonBodyAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    // --- Connexion ---

    [Fact]
    public async Task WebLogin_SetsHardenedCookie_AndKeepsTokenOutOfBody()
    {
        var email = await RegisterAsync();

        var response = await WebLoginAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = RefreshCookie(response);
        cookie.Should().NotBeNull();
        cookie!.Value.Value.Should().NotBeNullOrWhiteSpace();
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(Microsoft.Net.Http.Headers.SameSiteMode.Strict);
        cookie.Path.Value.Should().Be("/api/v1/auth");
        // Expiration alignée sur celle du jeton (Jwt:RefreshTokenDays = 7).
        cookie.Expires.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));

        var body = await JsonBodyAsync(response);
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
        body.TryGetProperty("refreshTokenExpiresAtUtc", out _).Should().BeFalse();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MobileLogin_WithoutTransportHeader_ReturnsTokenInBody_AndSetsNoCookie()
    {
        var email = await RegisterAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, "Test1234!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        RefreshCookie(response).Should().BeNull();
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        result!.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    // --- Rafraîchissement ---

    [Fact]
    public async Task Refresh_WithCookieOnly_RotatesCookie_AndKeepsTokenOutOfBody()
    {
        var email = await RegisterAsync();
        var original = RefreshCookie(await WebLoginAsync(email))!.Value.Value!;

        var response = await PostWithCookieAsync("/api/v1/auth/refresh", original);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = RefreshCookie(response);
        rotated.Should().NotBeNull();
        rotated!.Value.Value.Should().NotBe(original);
        rotated.HttpOnly.Should().BeTrue();

        var body = await JsonBodyAsync(response);
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
        body.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Refresh_PrefersCookieOverBody()
    {
        var email = await RegisterAsync();
        var cookieToken = RefreshCookie(await WebLoginAsync(email))!.Value.Value!;

        var response = await PostWithCookieAsync(
            "/api/v1/auth/refresh", cookieToken, new { refreshToken = "jeton-du-corps-ignore" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        RefreshCookie(response).Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_WithBodyOnly_StaysOnTheMobileChannel()
    {
        var email = await RegisterAsync();
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, "Test1234!"));
        var original = await login.Content.ReadFromJsonAsync<AuthResultDto>();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = original!.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        RefreshCookie(response).Should().BeNull();
        var renewed = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        renewed!.RefreshToken.Should().NotBeNullOrWhiteSpace().And.NotBe(original.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithoutCookieOrBody_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync("/api/v1/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithInvalidCookie_ReturnsUnauthorized_AndClearsCookie()
    {
        var response = await PostWithCookieAsync("/api/v1/auth/refresh", "ceci-n-est-pas-un-jeton-valide");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var cleared = RefreshCookie(response);
        cleared.Should().NotBeNull();
        cleared!.Value.Value.Should().BeEmpty();
        cleared.Path.Value.Should().Be("/api/v1/auth");
        cleared.Expires.Should().BeBefore(DateTimeOffset.UtcNow);
    }

    // --- Déconnexion ---

    [Fact]
    public async Task Logout_ClearsCookie_AndRevokesTheSession()
    {
        var email = await RegisterAsync();
        var token = RefreshCookie(await WebLoginAsync(email))!.Value.Value!;

        var logout = await PostWithCookieAsync("/api/v1/auth/logout", token);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cleared = RefreshCookie(logout);
        cleared.Should().NotBeNull();
        cleared!.Value.Value.Should().BeEmpty();
        cleared.Path.Value.Should().Be("/api/v1/auth");
        cleared.Expires.Should().BeBefore(DateTimeOffset.UtcNow);

        // Un cookie volé avant la déconnexion ne sert plus à rien.
        var replay = await PostWithCookieAsync("/api/v1/auth/refresh", token);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesTokensAlreadyRotatedFromTheSameLogin()
    {
        var email = await RegisterAsync();
        var first = RefreshCookie(await WebLoginAsync(email))!.Value.Value!;
        var second = RefreshCookie(await PostWithCookieAsync("/api/v1/auth/refresh", first))!.Value.Value!;

        await PostWithCookieAsync("/api/v1/auth/logout", first);

        // La famille entière est révoquée, pas seulement le jeton présenté.
        var response = await PostWithCookieAsync("/api/v1/auth/refresh", second);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithoutSession_StillSucceeds()
    {
        var response = await _client.PostAsync("/api/v1/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
