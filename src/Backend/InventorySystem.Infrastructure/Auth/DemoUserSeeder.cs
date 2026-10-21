using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Comptes de démonstration, un par rôle, pour ouvrir l'application sans avoir à
/// créer un utilisateur à la main.
///
/// Trois comptes plutôt qu'un seul : les écrans changent selon le rôle (un Employé
/// ne voit ni les fournisseurs ni les boutons de création), et une démonstration
/// limitée à un rôle ne montrerait pas ces différences.
///
/// N'agit que si « Seed:DemoPassword » est renseigné. Cette clé est absente de
/// appsettings.json : un déploiement qui ne la définit pas ne crée aucun compte.
/// Ne jamais la renseigner sur une base contenant des données réelles — le mot de
/// passe est en clair dans la configuration, par construction.
/// </summary>
public static class DemoUserSeeder
{
    /// <summary>Domaine réservé par la RFC 2606 : ces adresses ne peuvent pas exister.</summary>
    private static readonly (string Email, string Role, string DisplayName)[] Comptes =
    {
        ("employe@inventaire.invalid",      "Employe",      "Émile Demo (Employé)"),
        ("gestionnaire@inventaire.invalid", "Gestionnaire", "Gaby Demo (Gestionnaire)"),
        ("admin@inventaire.invalid",        "Admin",        "Alex Demo (Admin)"),
    };

    /// <summary>Adresses des comptes de démonstration, conservés par la remise à zéro.</summary>
    public static IReadOnlyList<string> Emails { get; } = Comptes.Select(c => c.Email).ToArray();

    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var password = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
            return;

        foreach (var (email, role, displayName) in Comptes)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                logger.LogWarning(
                    "Rôle {Role} absent : le compte de démonstration {Email} n'a pas été créé.",
                    role, email);
                continue;
            }

            // Idempotent : un redémarrage ne doit ni dupliquer le compte ni ecraser
            // le mot de passe d'un utilisateur qui serait entre-temps devenu reel.
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                // Comptes créés avant que le verrouillage soit désactivé pour la démo.
                if (existing.LockoutEnabled)
                {
                    await userManager.SetLockoutEnabledAsync(existing, false);
                    await userManager.SetLockoutEndDateAsync(existing, null);
                }
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                logger.LogError(
                    "Échec de la création du compte de démonstration {Email} : {Errors}",
                    email, string.Join(" | ", createResult.Errors.Select(e => e.Description)));
                continue;
            }

            // Adresses et mot de passe sont publics : avec le verrouillage, cinq essais faux
            // suffiraient à bloquer la démo pour tous les visiteurs. La limite de débit par IP
            // (Program.cs) freine la force brute à la place. CreateAsync l'active d'office.
            await userManager.SetLockoutEnabledAsync(user, false);

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                logger.LogError(
                    "Compte de démonstration {Email} créé mais l'attribution du rôle {Role} a échoué : {Errors}",
                    email, role, string.Join(" | ", roleResult.Errors.Select(e => e.Description)));
                continue;
            }

            logger.LogInformation("Compte de démonstration créé : {Email} ({Role})", email, role);
        }
    }
}
