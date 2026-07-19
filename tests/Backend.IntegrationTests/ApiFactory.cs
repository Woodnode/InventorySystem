using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace InventorySystem.Backend.IntegrationTests;

/// <summary>
/// Démarre un vrai PostgreSQL éphémère (Testcontainers) et l'application complète
/// (WebApplicationFactory) — voir plan §10 : tests d'intégration contre une vraie
/// base plutôt que des mocks de repository. Partagée par toute la classe de tests
/// via <see cref="IntegrationTestCollection"/> pour ne payer le coût du conteneur qu'une fois.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Clé de test uniquement — jamais utilisée en dehors des tests (voir TokenService).</summary>
    public const string TestJwtKey = "integration-tests-only-signing-key-not-for-prod-use-32chars";

    /// <summary>
    /// Compte Admin pré-créé pour les tests : contourne volontairement le contrôleur
    /// (qui interdit de s'auto-enregistrer Admin — voir AuthController) puisqu'il s'agit
    /// ici de données de test, pas du flux d'auth lui-même (déjà couvert par AuthEndpointsTests).
    /// </summary>
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Test1234!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("inventory_tests")
        .WithUsername("inventory")
        .WithPassword("inventory")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Jwt:Key"] = TestJwtKey,
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Applique les migrations via un DbContext autonome, AVANT que le host ne démarre :
        // le seed des rôles dans Program.cs a besoin des tables déjà en place, et le host
        // se construit dès qu'on touche Services/Server (voir accès ci-dessous).
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using (var db = new AppDbContext(options))
            await db.Database.MigrateAsync();

        // Déclenche la construction réelle du host (Program.cs, y compris le seed des rôles),
        // maintenant que le schéma existe.
        _ = Server;

        using var scope = Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        await identity.CreateUserAsync(AdminEmail, AdminPassword, "Admin de test", "Admin");
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
