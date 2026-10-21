using InventorySystem.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Infrastructure.Persistence;

/// <summary>
/// Remise à zéro du site vitrine. Les comptes de démonstration sont publics, dont un Admin et un
/// Gestionnaire : sans elle, un visiteur pouvait renommer tout le catalogue par un import,
/// désactiver les entrepôts ou se créer des comptes Admin, et la démo restait vandalisée.
/// </summary>
/// <remarks>
/// Efface les données métier, les sessions et tous les comptes sauf ceux de démonstration et
/// l'Admin d'amorçage (<c>Seed:AdminEmail</c>), puis recharge le jeu de démonstration.
/// </remarks>
public static class DemoReset
{
    public static async Task ResetAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var kept = DemoUserSeeder.Emails
            .Append(configuration["Seed:AdminEmail"])
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!.ToUpperInvariant())
            .ToList();

        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            // Ordre imposé par les clés étrangères (mouvements et stocks pointent vers produits et entrepôts).
            await context.StockMovements.ExecuteDeleteAsync(cancellationToken);
            await context.Stocks.ExecuteDeleteAsync(cancellationToken);
            await context.Products.ExecuteDeleteAsync(cancellationToken);
            await context.Warehouses.ExecuteDeleteAsync(cancellationToken);
            await context.Suppliers.ExecuteDeleteAsync(cancellationToken);
            await context.RefreshTokens.ExecuteDeleteAsync(cancellationToken);
            // Rôles, connexions externes et jetons Identity suivent par cascade.
            await context.Users
                .Where(u => u.NormalizedEmail == null || !kept.Contains(u.NormalizedEmail))
                .ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        context.ChangeTracker.Clear();
        await DemoUserSeeder.SeedAsync(userManager, roleManager, configuration, logger);
        await DemoDataSeeder.SeedAsync(context, logger, cancellationToken);
    }
}
