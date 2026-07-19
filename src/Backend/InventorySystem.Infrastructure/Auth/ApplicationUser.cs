using Microsoft.AspNetCore.Identity;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Utilisateur ASP.NET Core Identity. Type framework : reste dans Infrastructure,
/// jamais référencé depuis Domain ou Application (voir plan §3, règle de dépendance).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
