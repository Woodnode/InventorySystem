using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;

namespace InventoryMobile.Infrastructure.Auth;

/// <summary>
/// Ajoute automatiquement l'en-tête <c>Authorization: Bearer</c> à chaque requête, et
/// rafraîchit le token de manière PROACTIVE (avant qu'il n'expire) plutôt que réactive
/// (retenter après un 401) : plus simple à implémenter correctement côté client HTTP
/// (pas besoin de cloner une requête déjà envoyée) et suffisant pour ce jalon.
///
/// Dépend uniquement de <see cref="ITokenStore"/> (pas de <see cref="IAuthService"/>) pour
/// éviter un cycle de DI : IAuthService -> IInventoryApi -> HttpClient -> ce handler.
/// </summary>
public sealed class AuthHeaderHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    private readonly ITokenStore _tokenStore;
    private readonly string _apiBaseUrl;

    public AuthHeaderHandler(ITokenStore tokenStore, string apiBaseUrl)
    {
        _tokenStore = tokenStore;
        _apiBaseUrl = apiBaseUrl;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var session = await _tokenStore.LoadAsync();

        if (session is not null && session.IsExpiredOrExpiringSoon(ExpiryMargin))
        {
            session = await TryRefreshAsync(session.RefreshToken, cancellationToken) ?? session;
        }

        if (session is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<AuthSession?> TryRefreshAsync(string refreshToken, CancellationToken ct)
    {
        await RefreshLock.WaitAsync(ct);
        try
        {
            // Une autre requête a peut-être déjà rafraîchi pendant l'attente du verrou.
            var latest = await _tokenStore.LoadAsync();
            if (latest is not null && !latest.IsExpiredOrExpiringSoon(ExpiryMargin))
            {
                return latest;
            }

            // Client HTTP brut (pas le Refit injecté, qui repasserait par ce handler) :
            // seul l'appel de rafraîchissement lui-même échappe au cycle d'auth.
            using var refreshClient = new HttpClient { BaseAddress = new Uri(_apiBaseUrl) };
            using var response = await refreshClient.PostAsJsonAsync(
                "auth/refresh", new RefreshTokenRequest(refreshToken), JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Refresh token révoqué/expiré : plus rien à faire ici que nettoyer — la
                // prochaine action utilisateur recevra un 401 et sera renvoyée au login.
                await _tokenStore.ClearAsync();
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResultResponse>(JsonOptions, ct);
            if (result is null) return null;

            var refreshed = new AuthSession(
                result.AccessToken, result.AccessTokenExpiresAtUtc, result.RefreshToken,
                result.Email, result.DisplayName, result.Roles);

            await _tokenStore.SaveAsync(refreshed);
            return refreshed;
        }
        finally
        {
            RefreshLock.Release();
        }
    }
}
