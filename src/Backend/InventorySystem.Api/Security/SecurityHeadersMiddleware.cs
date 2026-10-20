namespace InventorySystem.Api.Security;

/// <summary>
/// En-têtes de sécurité de l'API. Elle ne sert que du JSON et des fichiers à télécharger :
/// rien ne doit s'y exécuter ni s'y afficher dans un cadre.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        return next(context);
    }
}
