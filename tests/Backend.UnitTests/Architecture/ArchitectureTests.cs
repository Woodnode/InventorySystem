using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace InventorySystem.Backend.UnitTests.Architecture;

/// <summary>
/// Règles de dépendance de l'architecture propre. Un échec signifie qu'une couche s'appuie sur
/// une couche ou une bibliothèque qui ne la concerne pas : le message liste les types fautifs.
/// </summary>
public sealed class ArchitectureTests
{
    private const string DomainNs = "InventorySystem.Domain";
    private const string ApplicationNs = "InventorySystem.Application";
    private const string InfrastructureNs = "InventorySystem.Infrastructure";
    private const string ApiNs = "InventorySystem.Api";

    private static readonly Assembly Domain = typeof(InventorySystem.Domain.Entities.Product).Assembly;
    private static readonly Assembly Application = typeof(InventorySystem.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(InventorySystem.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(InventorySystem.Api.Controllers.ProductsController).Assembly;

    private static readonly Assembly[] Toutes = [Domain, Application, Infrastructure, Api];

    private static void Verifier(TestResult resultat, string regle)
        => resultat.IsSuccessful.Should().BeTrue(
            $"{regle}. Types fautifs : {string.Join(", ", resultat.FailingTypeNames ?? [])}");

    // --- Domain : ne dépend de rien ---

    [Theory]
    [InlineData(ApplicationNs)]
    [InlineData(InfrastructureNs)]
    [InlineData(ApiNs)]
    public void EtantDonneLeDomaine_QuandAnalyserSesDependances_AlorsAucuneCoucheExterieure(string couche)
        => Verifier(Types.InAssembly(Domain).ShouldNot().HaveDependencyOn(couche).GetResult(),
            $"Domain ne doit pas dépendre de {couche}");

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("MediatR")]
    [InlineData("FluentValidation")]
    [InlineData("Npgsql")]
    public void EtantDonneLeDomaine_QuandAnalyserSesDependances_AlorsAucunCadriciel(string bibliotheque)
        => Verifier(Types.InAssembly(Domain).ShouldNot().HaveDependencyOn(bibliotheque).GetResult(),
            $"Domain ne doit pas dépendre de {bibliotheque}");

    [Fact]
    public void EtantDonneLAssemblageDuDomaine_QuandLireSesReferences_AlorsSeulementLaBibliothequeDeBase()
    {
        var references = Domain.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        references.Should().OnlyContain(n => n == "System.Runtime" || n.StartsWith("System.") || n == "netstandard",
            "le domaine ne référence aucun paquet ni aucun autre projet");
    }

    // --- Application : dépend du domaine et d'abstractions seulement ---

    [Theory]
    [InlineData(InfrastructureNs)]
    [InlineData(ApiNs)]
    public void EtantDonneLApplication_QuandAnalyserSesDependances_AlorsNiInfrastructureNiApi(string couche)
        => Verifier(Types.InAssembly(Application).ShouldNot().HaveDependencyOn(couche).GetResult(),
            $"Application ne doit pas dépendre de {couche} (inversion des dépendances)");

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Npgsql")]
    [InlineData("ClosedXML")]
    [InlineData("System.IdentityModel.Tokens.Jwt")]
    public void EtantDonneLApplication_QuandAnalyserSesDependances_AlorsAucuneTechnologieDInfrastructure(string bibliotheque)
        => Verifier(Types.InAssembly(Application).ShouldNot().HaveDependencyOn(bibliotheque).GetResult(),
            $"Application ne doit pas dépendre de {bibliotheque}");

    [Fact]
    public void EtantDonneLAssemblageApplication_QuandLireSesReferencesDeProjet_AlorsSeulementLeDomaine()
    {
        var projets = Application.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("InventorySystem."));

        projets.Should().BeEquivalentTo(["InventorySystem.Domain"]);
    }

    // --- Infrastructure et Api ---

    [Fact]
    public void EtantDonneLInfrastructure_QuandAnalyserSesDependances_AlorsPasDeDependanceVersLApi()
        => Verifier(Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn(ApiNs).GetResult(),
            "Infrastructure ne doit pas dépendre de Api");

    [Fact]
    public void EtantDonneLesControleurs_QuandAnalyserLeursDependances_AlorsJamaisLInfrastructureDirectement()
        => Verifier(Types.InAssembly(Api)
                .That().HaveNameEndingWith("Controller")
                .ShouldNot().HaveDependencyOnAny(InfrastructureNs, "Microsoft.EntityFrameworkCore")
                .GetResult(),
            "un contrôleur passe par MediatR, jamais par un repository ou le DbContext");

    // --- Emplacement et forme des types ---

    [Fact]
    public void EtantDonneLesHandlers_QuandChercherLeurCouche_AlorsTousDansLApplication()
        => Verifier(Types.InAssemblies(Toutes)
                .That().HaveNameEndingWith("Handler").And().AreClasses()
                .Should().ResideInNamespace(ApplicationNs)
                .GetResult(),
            "les cas d'usage MediatR vivent dans Application");

    [Fact]
    public void EtantDonneLesValidateurs_QuandChercherLeurCouche_AlorsTousDansLApplicationEtScelles()
        => Verifier(Types.InAssemblies(Toutes)
                .That().HaveNameEndingWith("Validator").And().AreClasses()
                .Should().ResideInNamespace(ApplicationNs).And().BeSealed()
                .GetResult(),
            "les validateurs vivent dans Application et ne sont pas extensibles");

    [Fact]
    public void EtantDonneLesControleurs_QuandChercherLeurCouche_AlorsTousDansLApiEtScelles()
        => Verifier(Types.InAssemblies(Toutes)
                .That().HaveNameEndingWith("Controller").And().AreClasses()
                .Should().ResideInNamespace($"{ApiNs}.Controllers").And().BeSealed()
                .GetResult(),
            "les contrôleurs vivent dans Api.Controllers");

    [Fact]
    public void EtantDonneLesRepositoriesConcrets_QuandChercherLeurCouche_AlorsTousDansLInfrastructure()
        => Verifier(Types.InAssemblies(Toutes)
                .That().HaveNameEndingWith("Repository").And().AreNotInterfaces()
                .Should().ResideInNamespace(InfrastructureNs)
                .GetResult(),
            "les implémentations d'accès aux données vivent dans Infrastructure");

    [Fact]
    public void EtantDonneLesContratsDeRepository_QuandChercherLeurCouche_AlorsTousDansLApplication()
        => Verifier(Types.InAssemblies(Toutes)
                .That().HaveNameEndingWith("Repository").And().AreInterfaces()
                .Should().ResideInNamespace($"{ApplicationNs}.Common.Interfaces")
                .GetResult(),
            "les contrats appartiennent à la couche qui les consomme");

    [Fact]
    public void EtantDonneLesEntitesDuDomaine_QuandChercherUnSetterPublic_AlorsAucun()
    {
        // Les invariants (stock jamais négatif, nom obligatoire...) ne tiennent que si l'état ne
        // change qu'à travers les méthodes de l'entité.
        var setters = Domain.GetTypes()
            .Where(t => t.Namespace == $"{DomainNs}.Entities" && t.IsClass)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        setters.Should().BeEmpty();
    }

    [Fact]
    public void EtantDonneLesEntitesDuDomaine_QuandChercherUnConstructeurPublic_AlorsCreationParFabriqueSeulement()
    {
        var constructeurs = Domain.GetTypes()
            .Where(t => t.Namespace == $"{DomainNs}.Entities" && t.IsClass && !t.IsAbstract)
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .Select(t => t.Name)
            .ToList();

        constructeurs.Should().BeEmpty("une entité se crée par sa méthode Create, qui valide ses invariants");
    }

    [Fact]
    public void EtantDonneLesExceptionsDuDomaine_QuandChercherLeurBase_AlorsToutesDeriventDeDomainException()
        => Verifier(Types.InAssembly(Domain)
                .That().HaveNameEndingWith("Exception")
                .Should().Inherit(typeof(InventorySystem.Domain.Exceptions.DomainException))
                .Or().HaveName(nameof(InventorySystem.Domain.Exceptions.DomainException))
                .GetResult(),
            "le middleware traduit DomainException en 400 : une exception métier qui n'en dérive pas deviendrait une 500");
}
