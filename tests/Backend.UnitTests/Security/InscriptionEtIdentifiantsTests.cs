using System.Security.Claims;
using FluentAssertions;
using FluentValidation.TestHelper;
using InventorySystem.Api.Contracts;
using InventorySystem.Api.Controllers;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace InventorySystem.Backend.UnitTests.Security;

/// <summary>
/// Inscription : qui peut créer quel compte, et quand l'inscription libre est fermée (site
/// vitrine). Puis les règles de saisie des identifiants, premier filtre avant Identity.
/// </summary>
public sealed class InscriptionEtIdentifiantsTests
{
    private readonly Mock<ISender> _mediator = new();

    private AuthController Creer(bool inscriptionPublique, params string[] rolesConnectes)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:PublicEnabled"] = inscriptionPublique.ToString(),
            })
            .Build();
        var identite = rolesConnectes.Length == 0
            ? new ClaimsIdentity()
            : new ClaimsIdentity(rolesConnectes.Select(r => new Claim(ClaimTypes.Role, r)), "Bearer");
        _mediator
            .Setup(m => m.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResultDto("acces", DateTime.UtcNow, "refresh", "a@test.local", "A", ["Employe"]));

        return new AuthController(_mediator.Object, configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identite) },
            },
        };
    }

    private static RegisterRequest Demande(string role) => new("nouveau@test.local", "Test1234!", "Nouveau", role);

    [Theory]
    [InlineData("Admin")]
    [InlineData("Gestionnaire")]
    [InlineData("admin")]
    public async Task EtantDonneVisiteurAnonyme_QuandDemanderUnRoleEleve_AlorsInterditSansAppelerLeCasDUsage(string role)
    {
        var result = await Creer(inscriptionPublique: true).Register(Demande(role), CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        _mediator.Verify(m => m.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneVitrineFermee_QuandVisiteurSInscrit_AlorsRouteIntrouvable()
    {
        // 404 plutôt que 403 : sur la vitrine, l'inscription n'existe pas pour le public.
        var result = await Creer(inscriptionPublique: false).Register(Demande("Employe"), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
        _mediator.Verify(m => m.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneVitrineFermee_QuandAdminCreeUnCompte_AlorsCreationPossible()
    {
        var result = await Creer(inscriptionPublique: false, "Admin").Register(Demande("Gestionnaire"), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        _mediator.Verify(m => m.Send(
            It.Is<RegisterCommand>(c => c.Role == "Gestionnaire"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneVitrineFermee_QuandGestionnaireCreeUnEmploye_AlorsRouteIntrouvable()
    {
        // Seul l'Admin échappe à la fermeture : un Gestionnaire connecté reste un « public ».
        var result = await Creer(inscriptionPublique: false, "Gestionnaire").Register(Demande("Employe"), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task EtantDonneInscriptionLibreOuverte_QuandVisiteurSInscritEmploye_AlorsCompteCree()
    {
        var result = await Creer(inscriptionPublique: true).Register(Demande("Employe"), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    // --- Règles de saisie ---

    [Theory]
    [InlineData("court1A")]
    [InlineData("toutenminuscule1")]
    [InlineData("SANSCHIFFRE")]
    [InlineData("")]
    public void EtantDonneMotDePasseFaible_QuandValiderLInscription_AlorsRefuse(string motDePasse)
    {
        var result = new RegisterCommandValidator().TestValidate(new RegisterCommand("a@test.local", motDePasse, "A", "Employe"));

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void EtantDonneMotDePasseConforme_QuandValiderLInscription_AlorsAccepte()
    {
        var result = new RegisterCommandValidator().TestValidate(new RegisterCommand("a@test.local", "Solide2026", "A", "Employe"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("admin")]
    [InlineData("")]
    public void EtantDonneRoleInconnu_QuandValiderLInscription_AlorsRefuse(string role)
    {
        var result = new RegisterCommandValidator().TestValidate(new RegisterCommand("a@test.local", "Solide2026", "A", role));

        result.ShouldHaveValidationErrorFor(c => c.Role);
    }

    [Theory]
    [InlineData("pas-un-courriel")]
    [InlineData("")]
    public void EtantDonneCourrielInvalide_QuandValiderLInscription_AlorsRefuse(string email)
    {
        var result = new RegisterCommandValidator().TestValidate(new RegisterCommand(email, "Solide2026", "A", "Employe"));

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void EtantDonneCourrielDePlusDe256Caracteres_QuandValiderLInscription_AlorsRefuse()
    {
        var email = new string('a', 250) + "@test.local";

        var result = new RegisterCommandValidator().TestValidate(new RegisterCommand(email, "Solide2026", "A", "Employe"));

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Theory]
    [InlineData("", "Test1234!", "L'adresse courriel est obligatoire.")]
    [InlineData("pas-un-courriel", "Test1234!", "L'adresse courriel n'est pas valide.")]
    [InlineData("a@test.local", "", "Le mot de passe est obligatoire.")]
    public void EtantDonneConnexionIncomplete_QuandValider_AlorsMessageEnFrancais(string email, string motDePasse, string message)
    {
        var result = new LoginCommandValidator().TestValidate(new LoginCommand(email, motDePasse));

        result.Errors.Should().Contain(e => e.ErrorMessage == message);
    }

    [Fact]
    public void EtantDonneRefreshTokenVide_QuandValider_AlorsRefuse()
    {
        var result = new RefreshTokenCommandValidator().TestValidate(new RefreshTokenCommand(""));

        result.ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }
}
