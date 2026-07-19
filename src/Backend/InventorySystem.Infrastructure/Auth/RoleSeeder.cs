using Microsoft.AspNetCore.Identity;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Rôles fixes de l'application (voir plan §3.3 : Admin, Gestionnaire, Employé).
/// Créés au démarrage s'ils n'existent pas encore — appelé une fois depuis Program.cs.
/// </summary>
public static class RoleSeeder
{
    public static readonly string[] Roles = { "Admin", "Gestionnaire", "Employe" };

    public static async Task SeedAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }
}
