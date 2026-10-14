namespace InventorySystem.Api.Auth;

/// <summary>
/// Transport du refresh token vers le client web : un cookie httpOnly que le JavaScript de la
/// page ne peut pas lire. Le client mobile n'est pas concerné, il garde son jeton dans
/// SecureStorage et l'envoie dans le corps de /auth/refresh.
/// <para>
/// SameSite=Strict suffit : en production le front et l'API sont servis à la même adresse
/// (/api/v1 via le relais), et en développement le serveur Vite relaie /api vers l'API.
/// Le chemin limite l'envoi du cookie aux seuls endpoints d'authentification.
/// </para>
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "inventory_refresh";
    public const string Path = "/api/v1/auth";

    /// <summary>
    /// En-tête par lequel le client web demande, à la connexion, à recevoir le refresh token
    /// en cookie plutôt que dans le corps. Sans lui (client mobile), la réponse reste inchangée.
    /// </summary>
    public const string TransportHeader = "X-Refresh-Token-Transport";
    public const string CookieTransport = "cookie";

    public static bool IsRequestedBy(HttpRequest request)
        => string.Equals(request.Headers[TransportHeader], CookieTransport, StringComparison.OrdinalIgnoreCase);

    public static string? Read(HttpRequest request)
        => request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static void Append(HttpResponse response, string refreshToken, DateTime expiresAtUtc)
        => response.Cookies.Append(Name, refreshToken,
            BuildOptions(new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc))));

    /// <summary>Mêmes attributs qu'à la pose, sinon le navigateur ne supprime pas le cookie.</summary>
    public static void Delete(HttpResponse response)
        => response.Cookies.Delete(Name, BuildOptions(expires: null));

    public static CookieOptions BuildOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
    };
}
