namespace InventorySystem.Infrastructure.Auth;

/// <summary>Options liées à la section de configuration "Jwt" (voir appsettings.json).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Clé de signature symétrique. NE JAMAIS commiter de vraie valeur dans appsettings.json :
    /// à définir via `dotnet user-secrets set "Jwt:Key" "..."` en local, ou une variable
    /// d'environnement / un coffre-fort (Key Vault, etc.) en production (voir plan §6 et README).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
