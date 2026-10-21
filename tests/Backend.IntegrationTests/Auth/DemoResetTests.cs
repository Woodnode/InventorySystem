using System.Net;
using FluentAssertions;
using InventorySystem.Api.Demo;
using InventorySystem.Api.Security;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Auth;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventorySystem.Backend.IntegrationTests.Auth;

/// <summary>
/// Remise à zéro du site vitrine : ce qu'un visiteur fait avec les comptes de démonstration
/// publics (catalogue renommé, entrepôts désactivés, comptes Admin créés) disparaît.
/// Conteneur PostgreSQL propre à la classe : la remise à zéro efface les comptes des autres tests.
/// </summary>
public sealed class DemoResetTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DemoResetTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EtantDonneDemoVandalisee_QuandRemiseAZero_AlorsJeuDeDemonstrationRestaure()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();

        var productCount = await db.Products.CountAsync();
        var product = await db.Products.OrderBy(p => p.Id).FirstAsync();
        var originalName = product.Name;
        db.Entry(product).Property(p => p.Name).CurrentValue = "Catalogue vandalisé";
        await db.Warehouses.ExecuteUpdateAsync(s => s.SetProperty(w => w.IsActive, false));
        await db.SaveChangesAsync();
        await users.CreateAsync(new ApplicationUser { UserName = "intrus@exemple.com", Email = "intrus@exemple.com", DisplayName = "Intrus" }, "Intrus1234");

        await DemoReset.ResetAsync(db, users, services.GetRequiredService<RoleManager<ApplicationRole>>(),
            services.GetRequiredService<IConfiguration>(), NullLogger.Instance);

        db.ChangeTracker.Clear();
        (await db.Products.CountAsync()).Should().Be(productCount);
        (await db.Products.AnyAsync(p => p.Name == "Catalogue vandalisé")).Should().BeFalse();
        (await db.Products.AnyAsync(p => p.Name == originalName)).Should().BeTrue();
        (await db.Warehouses.CountAsync(w => w.IsActive)).Should().BeGreaterThan(0);
        (await users.FindByEmailAsync("intrus@exemple.com")).Should().BeNull();
        foreach (var email in DemoUserSeeder.Emails)
            (await users.FindByEmailAsync(email)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("2026-10-20T02:00:00Z", 4, 2)]
    [InlineData("2026-10-20T04:00:00Z", 4, 24)]
    [InlineData("2026-10-20T23:00:00Z", 4, 5)]
    public void EtantDonneHeureCourante_QuandCalculerProchaineRemise_AlorsDansLes24Heures(string now, int hour, int expectedHours)
    {
        DemoResetService.DelayUntilNext(DateTimeOffset.Parse(now), hour).Should().Be(TimeSpan.FromHours(expectedHours));
    }

    [Fact]
    public void EtantDonneDeuxAdressesDuMemeSlash64_QuandPartitionner_AlorsMemeCle()
    {
        // Un script IPv6 tirait une adresse neuve de son /64 à chaque requête : fenêtre vierge.
        ClientIpForwarding.PartitionKey(IPAddress.Parse("2001:db8:1:2:aaaa::1"))
            .Should().Be(ClientIpForwarding.PartitionKey(IPAddress.Parse("2001:db8:1:2:bbbb::2")))
            .And.Be("2001:db8:1:2::/64");
        ClientIpForwarding.PartitionKey(IPAddress.Parse("2001:db8:1:3::1"))
            .Should().NotBe(ClientIpForwarding.PartitionKey(IPAddress.Parse("2001:db8:1:2::1")));
        ClientIpForwarding.PartitionKey(IPAddress.Parse("::ffff:203.0.113.7")).Should().Be("203.0.113.7");
        ClientIpForwarding.PartitionKey((IPAddress?)null).Should().Be("inconnue");
    }
}
