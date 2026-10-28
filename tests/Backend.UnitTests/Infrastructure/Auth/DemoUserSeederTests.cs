using InventorySystem.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Infrastructure.Auth;

/// <summary>
/// Non-régression de l'audit de sécurité : docker-compose.yml surcharge Seed__DemoPassword
/// par une valeur vide pour que la base locale (données réelles) ne reçoive aucun compte de
/// démonstration, même si appsettings.Development.json en définit un.
/// </summary>
public sealed class DemoUserSeederTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager = new(
        new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
    private readonly Mock<RoleManager<ApplicationRole>> _roleManager = new(
        new Mock<IRoleStore<ApplicationRole>>().Object, null!, null!, null!, null!);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SeedAsync_WhenEmptyValueOverridesDevelopmentPassword_CreatesNoAccount(string overrideValue)
    {
        // Même empilement qu'au démarrage : appsettings.Development.json, puis la variable
        // d'environnement du compose, qui l'emporte.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = "Demo@2026!" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = overrideValue })
            .Build();

        await DemoUserSeeder.SeedAsync(_userManager.Object, _roleManager.Object, configuration, NullLogger.Instance);

        _userManager.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        _roleManager.Verify(r => r.RoleExistsAsync(It.IsAny<string>()), Times.Never);
    }
}
