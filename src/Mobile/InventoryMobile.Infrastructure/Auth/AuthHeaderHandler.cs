using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;

namespace InventoryMobile.Infrastructure.Auth;

/// <summary>
/// Ajoute l'en-tête Bearer, rafraîchit de façon proactive, et sur 401 tente
/// <b>une</b> fois un refresh + retry avant d'expirer la session.
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
    private readonly Lazy<IAuthService> _authService;
    private readonly Lazy<ISessionExpiredNotifier> _sessionExpired;
    private readonly string _apiBaseUrl;
    private readonly HttpMessageHandler? _refreshHandler;

    public AuthHeaderHandler(
        ITokenStore tokenStore,
        Lazy<IAuthService> authService,
        Lazy<ISessionExpiredNotifier> sessionExpired,
        string apiBaseUrl,
        HttpMessageHandler? refreshHandler = null)
    {
        _tokenStore = tokenStore;
        _authService = authService;
        _sessionExpired = sessionExpired;
        _apiBaseUrl = apiBaseUrl;
        // Optionnel : null en production (comportement HttpClient par défaut, inchangé),
        // injectable en test pour éviter un vrai appel réseau lors du refresh — voir
        // AuthHeaderHandlerTests (AUDIT.md M-2).
        _refreshHandler = refreshHandler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => await SendAuthenticatedAsync(request, cancellationToken, allowRetryOn401: true);

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpRequestMessage request, CancellationToken cancellationToken, bool allowRetryOn401)
    {
        // Permet un éventuel retry 401 (le body doit pouvoir être relu).
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        var session = await _tokenStore.LoadAsync();

        if (session is not null && session.IsExpiredOrExpiringSoon(ExpiryMargin))
            session = await TryRefreshAsync(session.RefreshToken, cancellationToken);

        if (session is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        if (!allowRetryOn401)
        {
            await ExpireSessionAsync();
            return response;
        }

        // 401 alors que le token ne semblait pas expiré : tenter un refresh + un seul retry.
        var current = await _tokenStore.LoadAsync();
        if (current is null)
        {
            await ExpireSessionAsync();
            return response;
        }

        response.Dispose();
        var refreshed = await TryRefreshAsync(current.RefreshToken, cancellationToken);
        if (refreshed is null)
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        using var retry = await CloneAsync(request, cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        return await SendAuthenticatedAsync(retry, cancellationToken, allowRetryOn401: false);
    }

    private async Task ExpireSessionAsync()
    {
        await _tokenStore.ClearAsync();
        _authService.Value.ApplySession(null);
        _sessionExpired.Value.NotifySessionExpired();
    }

    private async Task<AuthSession?> TryRefreshAsync(string refreshToken, CancellationToken ct)
    {
        await RefreshLock.WaitAsync(ct);
        try
        {
            var latest = await _tokenStore.LoadAsync();
            if (latest is not null && !latest.IsExpiredOrExpiringSoon(ExpiryMargin))
            {
                _authService.Value.ApplySession(latest);
                return latest;
            }

            using var refreshClient = _refreshHandler is not null
                ? new HttpClient(_refreshHandler, disposeHandler: false) { BaseAddress = new Uri(_apiBaseUrl) }
                : new HttpClient { BaseAddress = new Uri(_apiBaseUrl) };
            using var response = await refreshClient.PostAsJsonAsync(
                "auth/refresh", new RefreshTokenRequest(refreshToken), JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                await ExpireSessionAsync();
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResultResponse>(JsonOptions, ct);
            if (result is null)
            {
                await ExpireSessionAsync();
                return null;
            }

            var refreshed = new AuthSession(
                result.AccessToken, result.AccessTokenExpiresAtUtc, result.RefreshToken,
                result.Email, result.DisplayName, result.Roles);

            await _tokenStore.SaveAsync(refreshed);
            _authService.Value.ApplySession(refreshed);
            return refreshed;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(ct);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
