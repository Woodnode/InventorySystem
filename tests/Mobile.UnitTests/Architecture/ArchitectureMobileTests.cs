using FluentAssertions;

namespace InventoryMobile.Mobile.UnitTests.Architecture;

/// <summary>
/// Couches de l'application mobile : Application ne connaît que des contrats, ni Refit, ni
/// SQLite, ni l'Infrastructure ; c'est l'Infrastructure qui dépend d'elle.
/// </summary>
public sealed class ArchitectureMobileTests
{
    private static readonly System.Reflection.Assembly Application = typeof(InventoryMobile.Application.Auth.IAuthService).Assembly;
    private static readonly System.Reflection.Assembly Infrastructure = typeof(InventoryMobile.Infrastructure.Auth.AuthService).Assembly;

    [Theory]
    [InlineData("InventoryMobile.Infrastructure")]
    [InlineData("Refit")]
    [InlineData("SQLite-net")]
    [InlineData("Microsoft.AspNetCore.SignalR.Client")]
    public void EtantDonneLaCoucheApplication_QuandLireSesReferences_AlorsAucuneTechnologie(string interdit)
        => Application.GetReferencedAssemblies().Select(a => a.Name).Should().NotContain(interdit);

    [Fact]
    public void EtantDonneLInfrastructure_QuandLireSesReferences_AlorsElleDependDeLApplication()
        => Infrastructure.GetReferencedAssemblies().Select(a => a.Name).Should().Contain("InventoryMobile.Application");

    [Fact]
    public void EtantDonneLesContratsDeLApplication_QuandLesParcourir_AlorsTousSontDesInterfacesOuDesDonnees()
    {
        // La couche Application mobile ne porte pas d'implémentation technique.
        var classesAvecComportement = Application.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("InventoryMobile.Application") == true)
            .Where(t => !(t.GetMethod("<Clone>$") is not null)) // records
            .Where(t => !t.Name.StartsWith('<'))
            .Select(t => t.FullName)
            .ToList();

        classesAvecComportement.Should().BeEmpty();
    }
}
