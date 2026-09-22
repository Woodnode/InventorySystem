using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;
using InventoryMobile.Infrastructure.Auth;
using Moq;
using Xunit;

namespace InventoryMobile.Mobile.UnitTests.Auth;

/// <summary>
/// Couvre AuthHeaderHandler (401 -> refresh -> un seul retry) en isolation, sans réseau réel
/// — jusqu'ici totalement non testé côté mobile (voir AUDIT.md M-2). Le client HTTP utilisé
/// en interne pour l'appel de refresh est maintenant injectable (<c>refreshHandler</c>),
/// ajouté spécifiquement pour permettre ce test sans toucher au comportement de production
/// (le paramètre est optionnel et retombe sur un vrai <c>HttpClient</c> par défaut).
///
/// <see cref="FakeTokenStore"/> est un vrai petit store en mémoire (pas un Mock) : ce handler
/// relit le store à plusieurs reprises (y compris dans un appel récursif pour le retry), et un
/// Mock à séquence fixe serait trop fragile face à cet ordre d'appels — un store qui reflète
/// vraiment ce qui a été sauvegardé est à la fois plus simple et plus fidèle.
/// </summary>
public sealed class AuthHeaderHandlerTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const string ApiBaseUrl = "https://api.test.local/api/v1/";

    private readonly FakeTokenStore _tokenStore = new();
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<ISessionExpiredNotifier> _sessionExpired = new();

    private static AuthSession ActiveSession(string accessToken = "access-1", string refreshToken = "refresh-1")
        => new(accessToken, DateTime.UtcNow.AddMinutes(15), refreshToken, "user@test.local", "Jean Dupont", new[] { "Employe" });

    private static AuthSession ExpiringSoonSession(string accessToken = "access-1", string refreshToken = "refresh-1")
        => new(accessToken, DateTime.UtcNow.AddSeconds(10), refreshToken, "user@test.local", "Jean Dupont", new[] { "Employe" });

    private HttpClient CreateClient(QueueHttpMessageHandler inner, HttpMessageHandler? refreshHandler = null)
    {
        var handler = new AuthHeaderHandler(
            _tokenStore,
            new Lazy<IAuthService>(() => _authService.Object),
            new Lazy<ISessionExpiredNotifier>(() => _sessionExpired.Object),
            ApiBaseUrl,
            refreshHandler)
        {
            InnerHandler = inner,
        };
        return new HttpClient(handler);
    }

    [Fact]
    public async Task SendAsync_WithNoStoredSession_ForwardsRequestWithoutAuthorizationHeader()
    {
        var inner = new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(inner);

        await client.GetAsync("https://api.test.local/api/v1/products");

        inner.Requests.Single().Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithActiveSession_AttachesBearerTokenWithoutRefreshing()
    {
        _tokenStore.Session = ActiveSession(accessToken: "the-access-token");
        var inner = new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var refreshHandler = new QueueHttpMessageHandler();
        var client = CreateClient(inner, refreshHandler);

        await client.GetAsync("https://api.test.local/api/v1/products");

        inner.Requests.Single().Headers.Authorization.Should().BeEquivalentTo(
            new AuthenticationHeaderValue("Bearer", "the-access-token"));
        refreshHandler.Requests.Should().BeEmpty(); // pas d'appel réseau de refresh nécessaire
    }

    [Fact]
    public async Task SendAsync_WithExpiringSoonToken_ProactivelyRefreshesOverTheNetworkBeforeSendingRequest()
    {
        _tokenStore.Session = ExpiringSoonSession(refreshToken: "refresh-1");

        var refreshed = new AuthResultResponse(
            "fresh-access", DateTime.UtcNow.AddMinutes(15), "fresh-refresh",
            "user@test.local", "Jean Dupont", new[] { "Employe" });
        var refreshHandler = new QueueHttpMessageHandler(_ => JsonResponse(refreshed));
        var inner = new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(inner, refreshHandler);

        await client.GetAsync("https://api.test.local/api/v1/products");

        refreshHandler.Requests.Should().ContainSingle();
        inner.Requests.Single().Headers.Authorization.Should().BeEquivalentTo(
            new AuthenticationHeaderValue("Bearer", "fresh-access"));
        _tokenStore.Session!.AccessToken.Should().Be("fresh-access");
        _authService.Verify(a => a.ApplySession(It.Is<AuthSession>(x => x.AccessToken == "fresh-access")), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenProactiveRefreshFails_ExpiresSessionAndPropagatesUnauthenticatedRequest()
    {
        _tokenStore.Session = ExpiringSoonSession(refreshToken: "refresh-1");

        var refreshHandler = new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        // Le refresh échoue avant même l'envoi de la requête initiale (elle était expirée dès
        // le départ) : la session est effacée, la requête part sans en-tête Authorization, et
        // current == null au retour empêche toute tentative de retry (return anticipé).
        var inner = new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(inner, refreshHandler);

        var response = await client.GetAsync("https://api.test.local/api/v1/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _tokenStore.Session.Should().BeNull();
        _authService.Verify(a => a.ApplySession(null), Times.AtLeastOnce);
        _sessionExpired.Verify(s => s.NotifySessionExpired(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendAsync_On401WithLocallyValidToken_RetriesOnceWithoutInfiniteLoop()
    {
        // Le token local ne semble pas expiré : le handler ne déclenche PAS de refresh réseau
        // (il suppose qu'une requête concurrente l'a peut-être déjà fait), retente une seule
        // fois via la garde allowRetryOn401: false, puis abandonne — jamais de boucle infinie
        // même si le serveur continue de répondre 401.
        _tokenStore.Session = ActiveSession(refreshToken: "refresh-1");

        var refreshHandler = new QueueHttpMessageHandler();
        var inner = new QueueHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(inner, refreshHandler);

        var response = await client.GetAsync("https://api.test.local/api/v1/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        inner.Requests.Should().HaveCount(2); // requête initiale + un seul retry, jamais plus
        refreshHandler.Requests.Should().BeEmpty(); // pas de refresh réseau : token local jugé valide
        _sessionExpired.Verify(s => s.NotifySessionExpired(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendAsync_On401WithLocallyValidTokenThatRetrySucceeds_ReturnsRetriedResponse()
    {
        _tokenStore.Session = ActiveSession(refreshToken: "refresh-1");

        var refreshHandler = new QueueHttpMessageHandler();
        var inner = new QueueHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized), // ex. faux positif transitoire côté serveur
            _ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(inner, refreshHandler);

        var response = await client.GetAsync("https://api.test.local/api/v1/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _sessionExpired.Verify(s => s.NotifySessionExpired(), Times.Never);
    }

    [Fact]
    public async Task SendAsync_On401WithLocallyExpiringToken_RefreshesOverTheNetworkAndRetriesWithNewToken()
    {
        // Ré-audit M-2 : le seul autre test 401 (ci-dessus) tombe sur le court-circuit
        // "token local pas expiré" de TryRefreshAsync et ne déclenche donc jamais d'appel
        // réseau réel — la combinaison "401 réactif + vrai refresh réseau + retry avec le
        // nouveau token" n'était couverte par aucun test. Ici, le store est délibérément
        // piloté pour refléter une session encore valide à la vérification initiale (pas de
        // refresh proactif) mais expirante au moment du double-check anti-course dans
        // TryRefreshAsync (force l'appel réseau plutôt que le court-circuit).
        var active = ActiveSession(accessToken: "stale-access", refreshToken: "refresh-1");
        var expiringSoon = ExpiringSoonSession(accessToken: "stale-access", refreshToken: "refresh-1");
        _tokenStore.Session = active;
        _tokenStore.LoadSequence = new Queue<AuthSession?>([active, active, expiringSoon]);

        var refreshed = new AuthResultResponse(
            "fresh-access", DateTime.UtcNow.AddMinutes(15), "fresh-refresh",
            "user@test.local", "Jean Dupont", new[] { "Employe" });
        var refreshHandler = new QueueHttpMessageHandler(_ => JsonResponse(refreshed));
        var inner = new QueueHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized), // requête initiale
            _ => new HttpResponseMessage(HttpStatusCode.OK)); // retry, avec le nouveau token
        var client = CreateClient(inner, refreshHandler);

        var response = await client.GetAsync("https://api.test.local/api/v1/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshHandler.Requests.Should().ContainSingle(); // le vrai appel réseau a bien eu lieu
        inner.Requests.Should().HaveCount(2);
        inner.Requests[1].Headers.Authorization.Should().BeEquivalentTo(
            new AuthenticationHeaderValue("Bearer", "fresh-access"));
        _tokenStore.Session!.AccessToken.Should().Be("fresh-access");
        _sessionExpired.Verify(s => s.NotifySessionExpired(), Times.Never);
    }

    private static HttpResponseMessage JsonResponse(AuthResultResponse body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, CamelCase), Encoding.UTF8, "application/json"),
    };
}

/// <summary>Faux ITokenStore en mémoire — voir la remarque en tête de fichier sur pourquoi
/// pas un Mock à séquence fixe ici.</summary>
internal sealed class FakeTokenStore : ITokenStore
{
    public AuthSession? Session { get; set; }

    /// <summary>
    /// File optionnelle : si non vide, chaque LoadAsync() en dépile une valeur (et la reflète
    /// dans Session) au lieu de renvoyer Session tel quel. Sert à simuler qu'un même appelant
    /// voit le store évoluer entre deux lectures successives (ex. AuthHeaderHandler relit le
    /// store à la fois pour la vérification initiale ET pour le double-check anti-course dans
    /// TryRefreshAsync — voir SendAsync_On401WithLocallyExpiringToken_RefreshesOverTheNetwork).
    /// Une fois vidée, retombe sur Session normalement (donc reflète bien SaveAsync ensuite).
    /// </summary>
    public Queue<AuthSession?>? LoadSequence { get; set; }

    public Task SaveAsync(AuthSession session)
    {
        Session = session;
        return Task.CompletedTask;
    }

    public Task<AuthSession?> LoadAsync()
    {
        if (LoadSequence is { Count: > 0 })
            Session = LoadSequence.Dequeue();

        return Task.FromResult(Session);
    }

    public Task ClearAsync()
    {
        Session = null;
        return Task.CompletedTask;
    }
}

/// <summary>Handler de test : renvoie les réponses fournies dans l'ordre, une par requête reçue.</summary>
internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders;

    public List<HttpRequestMessage> Requests { get; } = [];

    public QueueHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responders)
    {
        _responders = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responders);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responders.Count == 0)
            throw new InvalidOperationException("Aucune réponse en file pour cette requête de test.");

        return Task.FromResult(_responders.Dequeue()(request));
    }
}
