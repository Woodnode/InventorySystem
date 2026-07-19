using System.Reflection;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
}
