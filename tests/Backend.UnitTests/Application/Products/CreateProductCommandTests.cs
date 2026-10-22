using FluentAssertions;
using FluentValidation.TestHelper;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Commands;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Products;

/// <summary>Création d'un produit : règles de saisie, puis produit et stock initial créés ensemble.</summary>
public sealed class CreateProductCommandTests
{
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<ISupplierRepository> _suppliers = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStockRepository> _stocks = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Guid _entrepot = Guid.NewGuid();
    private readonly Guid _fournisseur = Guid.NewGuid();

    public CreateProductCommandTests()
    {
        _warehouses.Setup(w => w.ExistsAsync(_entrepot, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _suppliers.Setup(s => s.ExistsAsync(_fournisseur, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private CreateProductCommand Commande(
        string sku = "LIV-0200", string nom = "Le Petit Prince", int seuil = 3, int quantite = 10,
        Guid? entrepot = null, Guid? fournisseur = null, int boites = 2, int copiesParBoite = 5,
        string? commentaire = null)
        => new(sku, nom, "Conte", seuil, entrepot ?? _entrepot, quantite, fournisseur,
            "PRJ-1", "Folio", "12", "Roman", 1943, 180.5m, "Gallimard",
            "A", "4", "P-7", boites, copiesParBoite,
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), null, "Diffusion Nord", null, commentaire);

    private CreateProductCommandValidator Validateur() => new(_warehouses.Object, _suppliers.Object);

    [Fact]
    public async Task EtantDonneCommandeComplete_QuandValider_AlorsAcceptee()
    {
        var result = await Validateur().TestValidateAsync(Commande(fournisseur: _fournisseur));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("AB")]
    [InlineData("CODE-DE-PLUS-DE-TRENTE-DEUX-CARACTERES")]
    public async Task EtantDonneSkuInvalide_QuandValider_AlorsRefuse(string sku)
    {
        var result = await Validateur().TestValidateAsync(Commande(sku: sku));

        result.ShouldHaveValidationErrorFor(c => c.Sku);
    }

    [Fact]
    public async Task EtantDonneNomVide_QuandValider_AlorsRefuse()
    {
        var result = await Validateur().TestValidateAsync(Commande(nom: ""));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public async Task EtantDonneValeurNegative_QuandValider_AlorsRefusee(int seuil, int quantite, int boites, int copies)
    {
        var result = await Validateur().TestValidateAsync(Commande(seuil: seuil, quantite: quantite, boites: boites, copiesParBoite: copies));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task EtantDonneEntrepotInconnu_QuandValider_AlorsMessageExplicite()
    {
        var result = await Validateur().TestValidateAsync(Commande(entrepot: Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(c => c.WarehouseId).WithErrorMessage("L'entrepôt spécifié n'existe pas.");
    }

    [Fact]
    public async Task EtantDonneFournisseurInconnu_QuandValider_AlorsMessageExplicite()
    {
        var result = await Validateur().TestValidateAsync(Commande(fournisseur: Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(c => c.SupplierId).WithErrorMessage("Le fournisseur spécifié n'existe pas.");
    }

    [Fact]
    public async Task EtantDonneSansFournisseur_QuandValider_AlorsAccepteSansInterrogerLesFournisseurs()
    {
        var result = await Validateur().TestValidateAsync(Commande());

        result.ShouldNotHaveValidationErrorFor(c => c.SupplierId);
        _suppliers.Verify(s => s.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneCommentaireDePlusDe1000Caracteres_QuandValider_AlorsRefuse()
    {
        var result = await Validateur().TestValidateAsync(Commande(commentaire: new string('c', 1001)));

        result.ShouldHaveValidationErrorFor(c => c.Comment);
    }

    [Fact]
    public async Task EtantDonneCommandeValide_QuandCreer_AlorsProduitEtStockInitialEnregistresEnsemble()
    {
        Product? produit = null;
        Stock? stock = null;
        _products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((p, _) => produit = p);
        _stocks.Setup(s => s.AddAsync(It.IsAny<Stock>(), It.IsAny<CancellationToken>()))
            .Callback<Stock, CancellationToken>((s, _) => stock = s);
        var handler = new CreateProductCommandHandler(_products.Object, _stocks.Object, _unitOfWork.Object);

        var id = await handler.Handle(Commande(sku: "liv-0201", fournisseur: _fournisseur), CancellationToken.None);

        produit.Should().NotBeNull();
        id.Should().Be(produit!.Id);
        produit.Sku.Value.Should().Be("LIV-0201");
        produit.SupplierId.Should().Be(_fournisseur);
        produit.WeightPerCopyGrams.Should().Be(180.5m);
        stock.Should().NotBeNull();
        stock!.ProductId.Should().Be(produit.Id);
        stock.WarehouseId.Should().Be(_entrepot);
        stock.Quantity.Should().Be(10);
        stock.Section.Should().Be("A");
        stock.BoxesCount.Should().Be(2);
        stock.DistributorName.Should().Be("Diffusion Nord");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneNomBlancQuiEchappeALaValidation_QuandCreer_AlorsLeDomaineRefuseSansRienEnregistrer()
    {
        // Dernier rempart : même appelé sans le pipeline de validation, le domaine refuse.
        var handler = new CreateProductCommandHandler(_products.Object, _stocks.Object, _unitOfWork.Object);

        var act = () => handler.Handle(Commande(nom: "   "), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
