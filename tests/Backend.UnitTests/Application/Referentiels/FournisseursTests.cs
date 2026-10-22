using FluentAssertions;
using FluentValidation.TestHelper;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Suppliers.Commands;
using InventorySystem.Application.Suppliers.Dtos;
using InventorySystem.Application.Suppliers.Queries;
using InventorySystem.Domain.Entities;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Referentiels;

/// <summary>Fournisseurs : création, modification, désactivation (jamais de suppression) et liste.</summary>
public sealed class FournisseursTests
{
    private readonly Mock<ISupplierRepository> _suppliers = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private Supplier Existant(string nom = "Diffusion Nord")
    {
        var fournisseur = Supplier.Create(nom, "contact@diffusion-nord.example", "418 555-0142");
        _suppliers.Setup(s => s.ExistsAsync(fournisseur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _suppliers.Setup(s => s.GetByIdAsync(fournisseur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(fournisseur);
        return fournisseur;
    }

    [Fact]
    public void EtantDonneFournisseurComplet_QuandValiderLaCreation_AlorsAccepte()
    {
        var result = new CreateSupplierCommandValidator().TestValidate(
            new CreateSupplierCommand("Diffusion Nord", "contact@diffusion-nord.example", "418 555-0142"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EtantDonneSeulementUnNom_QuandValiderLaCreation_AlorsAccepte()
    {
        var result = new CreateSupplierCommandValidator().TestValidate(new CreateSupplierCommand("Diffusion Nord", null, null));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "a@b.example", null, "Name")]
    [InlineData("Nom", "pas-un-courriel", null, "ContactEmail")]
    [InlineData("Nom", null, "0123456789012345678901234567890", "Phone")]
    public void EtantDonneChampInvalide_QuandValiderLaCreation_AlorsRefuse(string nom, string? email, string? telephone, string champ)
    {
        var result = new CreateSupplierCommandValidator().TestValidate(new CreateSupplierCommand(nom, email, telephone));

        result.ShouldHaveValidationErrorFor(champ);
    }

    [Fact]
    public void EtantDonneNomDePlusDe200Caracteres_QuandValiderLaCreation_AlorsRefuse()
    {
        var result = new CreateSupplierCommandValidator().TestValidate(new CreateSupplierCommand(new string('n', 201), null, null));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public async Task EtantDonneCommandeValide_QuandCreer_AlorsFournisseurActifEnregistre()
    {
        Supplier? cree = null;
        _suppliers.Setup(s => s.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => cree = s);

        var id = await new CreateSupplierCommandHandler(_suppliers.Object, _unitOfWork.Object)
            .Handle(new CreateSupplierCommand("Diffusion Nord", null, "418 555-0142"), default);

        cree!.Id.Should().Be(id);
        cree.IsActive.Should().BeTrue();
        cree.Phone.Should().Be("418 555-0142");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneFournisseurInconnu_QuandValiderLaModification_AlorsIntrouvable()
    {
        var result = await new UpdateSupplierCommandValidator(_suppliers.Object)
            .TestValidateAsync(new UpdateSupplierCommand(Guid.NewGuid(), "Nom", null, null));

        result.ShouldHaveValidationErrorFor(c => c.Id).WithErrorMessage("Fournisseur introuvable.");
    }

    [Fact]
    public async Task EtantDonneFournisseurExistant_QuandModifier_AlorsCoordonneesRemplacees()
    {
        var fournisseur = Existant();

        await new UpdateSupplierCommandHandler(_suppliers.Object, _unitOfWork.Object)
            .Handle(new UpdateSupplierCommand(fournisseur.Id, "Diffusion Sud", null, null), default);

        fournisseur.Name.Should().Be("Diffusion Sud");
        fournisseur.ContactEmail.Should().BeNull("un champ vidé dans le formulaire est effacé");
        _suppliers.Verify(s => s.Update(fournisseur), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneFournisseurInconnu_QuandValiderLActivation_AlorsIntrouvable()
    {
        var result = await new SetSupplierActiveCommandValidator(_suppliers.Object)
            .TestValidateAsync(new SetSupplierActiveCommand(Guid.NewGuid(), false));

        result.ShouldHaveValidationErrorFor(c => c.Id).WithErrorMessage("Fournisseur introuvable.");
    }

    [Fact]
    public async Task EtantDonneFournisseurActif_QuandDesactiverPuisReactiver_AlorsEtatSuivi()
    {
        var fournisseur = Existant();
        var handler = new SetSupplierActiveCommandHandler(_suppliers.Object, _unitOfWork.Object);

        await handler.Handle(new SetSupplierActiveCommand(fournisseur.Id, false), default);
        fournisseur.IsActive.Should().BeFalse();

        await handler.Handle(new SetSupplierActiveCommand(fournisseur.Id, true), default);
        fournisseur.IsActive.Should().BeTrue();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EtantDonneFournisseursActifsEtInactifs_QuandLister_AlorsTousRenvoyesAvecLeurEtat()
    {
        // Les écrans filtrent eux-mêmes : la page Fournisseurs doit montrer les inactifs pour les réactiver.
        var actif = Supplier.Create("Actif", null, null);
        var inactif = Supplier.Create("Inactif", "x@y.example", null);
        inactif.Deactivate();
        _suppliers.Setup(s => s.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([actif, inactif]);

        var result = await new GetSuppliersQueryHandler(_suppliers.Object).Handle(new GetSuppliersQuery(), default);

        result.Should().BeEquivalentTo([
            new SupplierDto(actif.Id, "Actif", null, null, true),
            new SupplierDto(inactif.Id, "Inactif", "x@y.example", null, false),
        ]);
        _suppliers.Verify(s => s.ListAsync(1_000, It.IsAny<CancellationToken>()));
    }
}
