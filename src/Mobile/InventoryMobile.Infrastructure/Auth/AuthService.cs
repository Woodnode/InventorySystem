using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;

namespace InventoryMobile.Infrastructure.Auth;

/// <summary>Implémentation de <see cref="IAuthService"/> adossée à l'API réelle.</summary>
public sealed class AuthService : IAuthService
{
    /// <summary>
    /// Délai maximal accordé à la révocation serveur : la déconnexion locale ne doit jamais
    /// rester bloquée derrière un réseau lent ou absent.
    /// </summary>
    private static readonly TimeSpan LogoutTimeout = TimeSpan.FromSeconds(5);

    private readonly IInventoryApi _api;
    private readonly ITokenStore _tokenStore;

    public AuthService(IInventoryApi api, ITokenStore tokenStore)
    {
        _api = api;
        _tokenStore = tokenStore;
    }

    public AuthSession? CurrentSession { get; private set; }

    public void ApplySession(AuthSession? session) => CurrentSession = session;

    public async Task<AuthSession?> RestoreSessionAsync()
    {
        CurrentSession = await _tokenStore.LoadAsync();
        return CurrentSession;
    }

    public async Task<AuthSession> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var result = await _api.LoginAsync(new LoginRequest(email, password), ct);

        var session = new AuthSession(
            result.AccessToken, result.AccessTokenExpiresAtUtc, result.RefreshToken,
            result.Email, result.DisplayName, result.Roles);

        await _tokenStore.SaveAsync(session);
        CurrentSession = session;
        return session;
    }

    public async Task LogoutAsync()
    {
        // Le stockage fait foi : AuthHeaderHandler y enregistre le jeton après chaque rotation.
        var refreshToken = (await _tokenStore.LoadAsync() ?? CurrentSession)?.RefreshToken;

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            // Au mieux : hors ligne ou serveur indisponible, la session est quand même fermée
            // sur l'appareil ; le jeton expirera alors de lui-même côté serveur.
            using var timeout = new CancellationTokenSource(LogoutTimeout);
            try
            {
                await _api.LogoutAsync(new RefreshTokenRequest(refreshToken), timeout.Token);
            }
            catch (Exception)
            {
                // Volontairement ignoré (voir ci-dessus).
            }
        }

        await _tokenStore.ClearAsync();
        CurrentSession = null;
    }
}
