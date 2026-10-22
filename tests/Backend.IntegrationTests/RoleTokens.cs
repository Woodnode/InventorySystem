using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySystem.Backend.IntegrationTests;

/// <summary>
/// Jetons d'accès par rôle, obtenus par la vraie route de connexion. Un compte est créé par
/// rôle et par fabrique (donc par base), puis réutilisé : les tests n'ont pas à dépendre des
/// comptes de démonstration, et le budget de la limite de débit n'est entamé qu'une fois.
/// </summary>
public static class RoleTokens
{
    public const string Employe = "Employe";
    public const string Gestionnaire = "Gestionnaire";
    public const string Admin = "Admin";
    public const string Password = "Test1234!";

    private static readonly ConcurrentDictionary<(ApiFactory Factory, string Role), Lazy<Task<string>>> Cache = new();

    public static Task<string> GetAsync(ApiFactory factory, string role)
        => Cache.GetOrAdd((factory, role), key => new Lazy<Task<string>>(() => CreateAsync(key.Factory, key.Role))).Value;

    /// <summary>Crée un compte du rôle demandé et renvoie la réponse complète de connexion.</summary>
    public static async Task<AuthResultDto> CreateUserAndLoginAsync(ApiFactory factory, string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            var created = await identity.CreateUserAsync(email, Password, $"{role} de test", role);
            created.Succeeded.Should().BeTrue(string.Join(" | ", created.Errors));
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResultDto>())!;
    }

    private static async Task<string> CreateAsync(ApiFactory factory, string role)
        => (await CreateUserAndLoginAsync(factory, role)).AccessToken;

    /// <summary>Envoie une requête avec le jeton Bearer donné (ou sans en-tête si null).</summary>
    public static async Task<HttpResponseMessage> SendAsync(
        this HttpClient client, HttpMethod method, string url, string? token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
