using FluentAssertions;
using FluentValidation.TestHelper;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Movements.Commands;
using InventorySystem.Domain.Entities;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Movements;

/// <summary>
/// Règles vérifiées avant toute écriture : produit et entrepôts existants, quantité positive,
/// destination présente seulement pour un transfert, et différente de la source.
/// </summary>
public sealed class RecordMovementCommandValidatorTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();

    private readonly Guid _produit = Guid.NewGuid();
    private readonly Guid _source = Guid.NewGuid();
    private readonly Guid _destination = Guid.NewGuid();

    public RecordMovementCommandValidatorTests()
    {
        _products.Setup(p => p.ExistsAsync(_produit, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _warehouses.Setup(w => w.ExistsAsync(_source, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _warehouses.Setup(w => w.ExistsAsync(_destination, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private RecordMovementCommandValidator Creer() => new(_products.Object, _warehouses.Object);

    private RecordMovementCommand Commande(
        MovementType type = MovementType.In, int quantite = 5, Guid? destination = null,
        Guid? produit = null, Guid? source = null, string? motif = "Réception")
        => new(produit ?? _produit, source ?? _source, type, quantite, motif, destination, Guid.NewGuid());

    [Theory]
    [InlineData(MovementType.In)]
    [InlineData(MovementType.Out)]
    public async Task EtantDonneEntreeOuSortieComplete_QuandValider_AlorsAcceptee(MovementType type)
    {
        var result = await Creer().TestValidateAsync(Commande(type));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task EtantDonneTransfertVersUnAutreEntrepot_QuandValider_AlorsAccepte()
    {
        var result = await Creer().TestValidateAsync(Commande(MovementType.Transfer, destination: _destination));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task EtantDonneProduitInconnu_QuandValider_AlorsProduitIntrouvable()
    {
        var result = await Creer().TestValidateAsync(Commande(produit: Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(c => c.ProductId).WithErrorMessage("Produit introuvable.");
    }

    [Fact]
    public async Task EtantDonneProduitVide_QuandValider_AlorsRefuseSansInterrogerLaBase()
    {
        var result = await Creer().TestValidateAsync(Commande(produit: Guid.Empty));

        result.ShouldHaveValidationErrorFor(c => c.ProductId);
    }

    [Fact]
    public async Task EtantDonneEntrepotSourceInconnu_QuandValider_AlorsMessageExplicite()
    {
        var result = await Creer().TestValidateAsync(Commande(source: Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(c => c.WarehouseId).WithErrorMessage("L'entrepôt source n'existe pas.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task EtantDonneQuantiteNulleOuNegative_QuandValider_AlorsRefusee(int quantite)
    {
        var result = await Creer().TestValidateAsync(Commande(quantite: quantite));

        result.ShouldHaveValidationErrorFor(c => c.Quantity);
    }

    [Fact]
    public async Task EtantDonneTypeHorsEnumeration_QuandValider_AlorsRefuse()
    {
        var result = await Creer().TestValidateAsync(Commande((MovementType)42));

        result.ShouldHaveValidationErrorFor(c => c.Type);
    }

    [Fact]
    public async Task EtantDonneMotifDePlusDe500Caracteres_QuandValider_AlorsRefuse()
    {
        var result = await Creer().TestValidateAsync(Commande(motif: new string('m', 501)));

        result.ShouldHaveValidationErrorFor(c => c.Reason);
    }

    [Fact]
    public async Task EtantDonneMotifDe500Caracteres_QuandValider_AlorsAccepte()
    {
        var result = await Creer().TestValidateAsync(Commande(motif: new string('m', 500)));

        result.ShouldNotHaveValidationErrorFor(c => c.Reason);
    }

    [Fact]
    public async Task EtantDonneTransfertSansDestination_QuandValider_AlorsDestinationExigee()
    {
        var result = await Creer().TestValidateAsync(Commande(MovementType.Transfer));

        result.ShouldHaveValidationErrorFor(c => c.ToWarehouseId)
            .WithErrorMessage("Un transfert doit préciser l'entrepôt de destination.");
    }

    [Theory]
    [InlineData(MovementType.In)]
    [InlineData(MovementType.Out)]
    public async Task EtantDonneDestinationSurUnMouvementSimple_QuandValider_AlorsRefusee(MovementType type)
    {
        var result = await Creer().TestValidateAsync(Commande(type, destination: _destination));

        result.ShouldHaveValidationErrorFor(c => c.ToWarehouseId)
            .WithErrorMessage("L'entrepôt de destination ne s'applique qu'à un transfert.");
    }

    [Fact]
    public async Task EtantDonneTransfertVersUnEntrepotInconnu_QuandValider_AlorsMessageExplicite()
    {
        var result = await Creer().TestValidateAsync(Commande(MovementType.Transfer, destination: Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(c => c.ToWarehouseId)
            .WithErrorMessage("L'entrepôt de destination n'existe pas.");
    }

    [Fact]
    public async Task EtantDonneTransfertVersLaSource_QuandValider_AlorsRefuse()
    {
        var result = await Creer().TestValidateAsync(Commande(MovementType.Transfer, destination: _source));

        result.Errors.Should().Contain(e => e.ErrorMessage == "L'entrepôt de destination doit être différent de l'entrepôt source.");
    }

    [Fact(Skip = "Bogue signalé : l'API accepte un mouvement dans un entrepôt désactivé (seuls les clients le masquent). Décision à prendre sur les sorties et transferts depuis un entrepôt fermé.")]
    public async Task EtantDonneEntrepotDesactive_QuandEnregistrerUnMouvement_AlorsRefuse()
    {
        _warehouses.Setup(w => w.GetByIdAsync(_source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Desactive());

        var result = await Creer().TestValidateAsync(Commande());

        result.ShouldHaveValidationErrorFor(c => c.WarehouseId);
    }

    private static Warehouse Desactive()
    {
        var entrepot = Warehouse.Create("Ancien dépôt", null);
        entrepot.Deactivate();
        return entrepot;
    }
}
