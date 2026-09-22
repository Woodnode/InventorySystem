using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Infrastructure.Auth;
using InventorySystem.Infrastructure.Export;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Infrastructure.Persistence.Repositories;
using InventorySystem.Infrastructure.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySystem.Infrastructure;

/// <summary>
/// Enregistrement des services d'infrastructure (DbContext PostgreSQL, repositories, UoW,
/// Identity, JWT). Application dépend d'abstractions ; c'est ici qu'on branche les
/// implémentations concrètes (DIP, voir plan §3.3/§4).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Résolu paresseusement via IServiceProvider (pas capturé dans une closure ici) :
        // WebApplicationFactory (tests d'intégration) applique ses overrides de config
        // (Testcontainers) au moment de construire le host, APRÈS que ce module d'enregistrement
        // se soit exécuté — capturer configuration.GetConnectionString(...) tout de suite
        // figerait la valeur par défaut d'appsettings.json au lieu de celle du conteneur éphémère.
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")
                ?? throw new InvalidOperationException(
                    "Chaîne de connexion 'ConnectionStrings:Postgres' manquante dans la configuration.");
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IExportService, ExportService>();
        services.AddMemoryCache();
        services.AddSingleton<IRefreshRotationCache, MemoryRefreshRotationCache>();

        // --- SignalR (alertes de stock bas temps réel — voir StockHub/StockNotifier) ---
        services.AddSignalR();
        services.AddScoped<IStockNotifier, StockNotifier>();

        // --- Identity (utilisateurs/rôles) ---
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Politique de mot de passe alignée sur RegisterCommandValidator (Application).
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;

                // Verrouillage anti brute-force (voir plan §6).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            // IdentityError.Description est en anglais par défaut et remonte tel quel
            // jusqu'au frontend (voir IdentityService.CreateUserAsync) — une UI française
            // afficherait sinon un mélange de langues sur les erreurs d'inscription.
            .AddErrorDescriber<FrenchIdentityErrorDescriber>();

        // --- JWT (options + services) ---
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        return services;
    }
}
