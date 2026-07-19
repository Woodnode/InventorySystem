using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Amorce le tout premier compte Admin d'une base neuve. L'inscription publique
/// (<c>RegisterCommand</c>) force volontairement le rôle "Employe" — personne ne peut
/// s'auto-attribuer Admin par l'API. Sans ce seeder, il n'existe donc aucun moyen de
/// créer le premier compte Admin autrement qu'en modifiant la base à la main.
///
/// N'agit que si :
///  - aucun utilisateur n'a déjà le rôle Admin (idempotent, sans effet après le premier lancement) ;
///  - les identifiants sont configurés via la section "Seed:AdminEmail"/"Seed:AdminPassword"
///    (à définir en local via <c>dotnet user-secrets</c>, jamais commités — voir README).
///
/// Si les identifiants ne sont pas configurés, ne crée rien et journalise un avertissement
/// expliquant comment procéder (dev-secrets ou promotion manuelle d'un compte existant).
/// Appelé une fois depuis Program.cs, après <see cref="RoleSeeder"/>.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
            return; // RoleSeeder n'a pas (encore) créé les rôles fixes — rien à faire ici.

        var existingAdmins = await userManager.GetUsersInRoleAsync("Admin");
        if (existingAdmins.Count > 0)
            return; // Déjà au moins un Admin : ne rien faire (évite tout effet de bord au redémarrage).

        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        var displayName = configuration["Seed:AdminDisplayName"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Aucun compte Admin n'existe et Seed:AdminEmail / Seed:AdminPassword ne sont pas " +
                "configurés : aucun compte Admin n'a été créé. Définissez-les via " +
                "'dotnet user-secrets set \"Seed:AdminEmail\" \"...\"' (et Seed:AdminPassword) " +
                "pour amorcer le premier compte au prochain démarrage, ou promouvez un compte " +
                "existant manuellement (voir README, section Démarrage local).");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Administrateur" : displayName,
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            logger.LogError(
                "Échec de la création du compte Admin initial ({Email}) : {Errors}",
                email, string.Join(" | ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, "Admin");
        if (!roleResult.Succeeded)
        {
            logger.LogError(
                "Compte Admin initial créé mais l'attribution du rôle a échoué ({Email}) : {Errors}",
                email, string.Join(" | ", roleResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Compte Admin initial créé : {Email}", email);
    }
}
