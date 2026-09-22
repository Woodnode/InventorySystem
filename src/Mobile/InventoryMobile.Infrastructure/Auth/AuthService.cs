using InventoryMobile.Application.Auth;
using InventoryMobile.Infrastructure.Api;

namespace InventoryMobile.Infrastructure.Auth;

/// <summary>Implémentation de <see cref="IAuthService"/> adossée à l'API réelle.</summary>
public sealed class AuthService : IAuthService
{
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
        await _tokenStore.ClearAsync();
        CurrentSession = null;
    }
}
