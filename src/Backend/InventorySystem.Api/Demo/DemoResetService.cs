using InventorySystem.Infrastructure.Auth;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace InventorySystem.Api.Demo;

/// <summary>
/// Remise à zéro quotidienne du site vitrine (voir <see cref="DemoReset"/>). Activée avec le jeu de
/// démonstration (<c>Seed:Demo</c>), à l'heure UTC <c>Seed:DemoResetHourUtc</c> (4 h par défaut).
/// </summary>
public sealed class DemoResetService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<DemoResetService> logger) : BackgroundService
{
    private readonly int _hourUtc = Math.Clamp(configuration.GetValue("Seed:DemoResetHourUtc", 4), 0, 23);

    /// <summary>Délai jusqu'à la prochaine remise à zéro, toujours dans les 24 heures.</summary>
    public static TimeSpan DelayUntilNext(DateTimeOffset now, int hourUtc)
    {
        var next = new DateTimeOffset(now.UtcDateTime.Date.AddHours(hourUtc), TimeSpan.Zero);
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(DelayUntilNext(DateTimeOffset.UtcNow, _hourUtc), stoppingToken);

            try
            {
                using var scope = scopes.CreateScope();
                var services = scope.ServiceProvider;
                await DemoReset.ResetAsync(
                    services.GetRequiredService<AppDbContext>(),
                    services.GetRequiredService<UserManager<ApplicationUser>>(),
                    services.GetRequiredService<RoleManager<ApplicationRole>>(),
                    configuration,
                    logger,
                    stoppingToken);
                logger.LogInformation("Données de démonstration remises à zéro.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Échec de la remise à zéro des données de démonstration.");
            }
        }
    }
}
