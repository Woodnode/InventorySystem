using Microsoft.AspNetCore.HttpOverrides;

namespace InventorySystem.Api.Security;

/// <summary>
/// Adresse IP réelle du visiteur. En production, la requête traverse le relais Cloudflare Pages
/// puis Caddy : sans ce réglage, l'API ne voit que l'adresse de Caddy, et une limite « par IP »
/// deviendrait une limite commune à tous les visiteurs.
/// </summary>
/// <remarks>
/// Le relais écrit l'IP du visiteur (CF-Connecting-IP) dans <see cref="HeaderName"/> en écrasant
/// toute valeur envoyée par le client ; Caddy transmet l'en-tête tel quel. L'API ne le croit que
/// s'il arrive d'une adresse privée (Caddy ou le réseau Docker), jamais d'Internet.
/// </remarks>
public static class ClientIpForwarding
{
    public const string HeaderName = "X-Real-Client-IP";

    public static IServiceCollection AddClientIpForwarding(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardedForHeaderName = HeaderName;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in new[] { "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "::1/128" })
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });

    /// <summary>Clé de partition des limiteurs : l'IP du client, ou une clé commune si elle est inconnue.</summary>
    public static string PartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "inconnue";
}
