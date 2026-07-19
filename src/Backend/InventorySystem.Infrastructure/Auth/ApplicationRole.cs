using Microsoft.AspNetCore.Identity;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>Rôle ASP.NET Core Identity. Valeurs utilisées : Admin, Gestionnaire, Employe.</summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }
}
