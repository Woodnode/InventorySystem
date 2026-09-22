using System.Reflection;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InventorySystem.Infrastructure.Persistence;

/// <summary>
/// DbContext EF Core (PostgreSQL). Sert aussi d'Unit of Work et de store ASP.NET Core
/// Identity (utilisateurs/rôles). Les configurations d'entités métier utilisent la Fluent
/// API (pas de Data Annotations sur le Domain, pour le garder découplé — voir plan §3.3).
/// Identity reste un détail Infrastructure : Domain et Application n'en savent rien (DIP).
/// </summary>
public sealed class AppDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Stock> Stocks => Set<Stock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // tables AspNetUsers/AspNetRoles/... (Identity)
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        await using var tx = await Database.BeginTransactionAsync(ct);
        try
        {
            await action(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Traduit les exceptions EF Core / Npgsql en <see cref="ConcurrencyConflictException"/>
    /// (Application) ICI, dans l'Infrastructure — pas dans le middleware Api, qui n'a alors
    /// plus besoin de connaître EF/Npgsql (voir AUDIT.md B-CA1). Les deux overloads sont
    /// interceptés (pas seulement celui à un seul paramètre CancellationToken) : Identity
    /// (UserManager/RoleManager) ou du code futur pourrait emprunter l'autre overload et
    /// contourner silencieusement la traduction sinon (voir ré-audit).
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesWithTranslationAsync(() => base.SaveChangesAsync(cancellationToken));

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => SaveChangesWithTranslationAsync(
            () => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken));

    private static async Task<int> SaveChangesWithTranslationAsync(Func<Task<int>> saveChanges)
    {
        try
        {
            return await saveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Seule Stock porte un token de concurrence (Version -> xmin, voir
            // StockConfiguration), donc ce message reste toujours correctement scopé.
            throw new ConcurrencyConflictException(
                "Le stock a été modifié entre-temps par une autre opération. Veuillez réessayer.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConcurrencyConflictException(BuildUniqueViolationMessage(ex));
        }
    }

    /// <summary>
    /// Plusieurs agrégats ont une contrainte unique (Product.Sku, Stock (ProductId,
    /// WarehouseId), StockMovement.ClientGuid, RefreshToken.TokenHash) : le message doit
    /// refléter celui réellement en cause, pas être codé en dur pour un seul d'entre eux
    /// (bug identifié au ré-audit — la traduction B-CA1 avait été généralisée à tout le
    /// DbContext mais le message était resté spécifique à Stock).
    /// </summary>
    private static string BuildUniqueViolationMessage(DbUpdateException ex)
    {
        var entityType = ex.Entries.Count > 0 ? ex.Entries[0].Entity.GetType() : null;

        if (entityType == typeof(Stock))
            return "Le stock a déjà été initialisé entre-temps par une autre opération. Veuillez réessayer.";
        if (entityType == typeof(Product))
            return "Ce SKU est déjà utilisé par un autre produit.";
        if (entityType == typeof(StockMovement))
            return "Ce mouvement a déjà été synchronisé (identifiant client déjà utilisé).";
        if (entityType == typeof(RefreshToken))
            return "Conflit sur la session : veuillez vous reconnecter.";

        return "Cette opération entre en conflit avec une donnée déjà existante. Veuillez réessayer.";
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
