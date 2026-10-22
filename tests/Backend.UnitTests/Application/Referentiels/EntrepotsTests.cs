using FluentAssertions;
using FluentValidation.TestHelper;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Stocks.Queries;
using InventorySystem.Application.Warehouses.Commands;
using InventorySystem.Application.Warehouses.Dtos;
using InventorySystem.Application.Warehouses.Queries;
using InventorySystem.Domain.Entities;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Referentiels;

/// <summary>Entrepôts : création, renommage, désactivation, liste, et répartition du stock par entrepôt.</summary>
public sealed class EntrepotsTests
{
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private Warehouse Existant()
    {
        var entrepot = Warehouse.Create("Entrepôt central", "1 rue du Port");
        _warehouses.Setup(w => w.ExistsAsync(entrepot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _warehouses.Setup(w => w.GetByIdAsync(entrepot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entrepot);
        return entrepot;
    }

    [Theory]
    [InlineData("", null, "Name")]
    [InlineData("   ", null, "Name")]
    public void EtantDonneNomVide_QuandValiderLaCreation_AlorsRefuse(string nom, string? adresse, string champ)
    {
        var result = new CreateWarehouseCommandValidator().TestValidate(new CreateWarehouseCommand(nom, adresse));

        result.ShouldHaveValidationErrorFor(champ);
    }

    [Fact]
    public void EtantDonneAdresseDePlusDe500Caracteres_QuandValiderLaCreation_AlorsRefusee()
    {
        var result = new CreateWarehouseCommandValidator().TestValidate(new CreateWarehouseCommand("Dépôt", new string('a', 501)));

        result.ShouldHaveValidationErrorFor(c => c.Address);
    }

    [Fact]
    public async Task EtantDonneCommandeValide_QuandCreer_AlorsEntrepotActifEnregistre()
    {
        Warehouse? cree = null;
        _warehouses.Setup(w => w.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()))
            .Callback<Warehouse, CancellationToken>((w, _) => cree = w);

        var id = await new CreateWarehouseCommandHandler(_warehouses.Object, _unitOfWork.Object)
            .Handle(new CreateWarehouseCommand("Réserve nord", null), default);

        cree!.Id.Should().Be(id);
        cree.IsActive.Should().BeTrue();
        cree.Address.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneEntrepotInconnu_QuandValiderLeRenommage_AlorsIntrouvable()
    {
        var result = await new UpdateWarehouseCommandValidator(_warehouses.Object)
            .TestValidateAsync(new UpdateWarehouseCommand(Guid.NewGuid(), "Nom", null));

        result.ShouldHaveValidationErrorFor(c => c.Id).WithErrorMessage("Entrepôt introuvable.");
    }

    [Fact]
    public async Task EtantDonneEntrepotExistant_QuandRenommer_AlorsNomEtAdresseRemplaces()
    {
        var entrepot = Existant();

        await new UpdateWarehouseCommandHandler(_warehouses.Object, _unitOfWork.Object)
            .Handle(new UpdateWarehouseCommand(entrepot.Id, "Entrepôt sud", "2 rue du Quai"), default);

        entrepot.Name.Should().Be("Entrepôt sud");
        entrepot.Address.Should().Be("2 rue du Quai");
        _warehouses.Verify(w => w.Update(entrepot), Times.Once);
    }

    [Fact]
    public async Task EtantDonneEntrepotInconnu_QuandValiderLActivation_AlorsIntrouvable()
    {
        var result = await new SetWarehouseActiveCommandValidator(_warehouses.Object)
            .TestValidateAsync(new SetWarehouseActiveCommand(Guid.NewGuid(), true));

        result.ShouldHaveValidationErrorFor(c => c.Id).WithErrorMessage("Entrepôt introuvable.");
    }

    [Fact]
    public async Task EtantDonneEntrepotActif_QuandDesactiver_AlorsConserveMaisInactif()
    {
        var entrepot = Existant();

        await new SetWarehouseActiveCommandHandler(_warehouses.Object, _unitOfWork.Object)
            .Handle(new SetWarehouseActiveCommand(entrepot.Id, false), default);

        entrepot.IsActive.Should().BeFalse();
        entrepot.Name.Should().Be("Entrepôt central", "désactiver ne touche à rien d'autre");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneEntrepots_QuandLister_AlorsDtoAvecEtatEtPlafondDeReferentiel()
    {
        var entrepot = Warehouse.Create("Boutique", "Place d'Youville");
        entrepot.Deactivate();
        _warehouses.Setup(w => w.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([entrepot]);

        var result = await new GetWarehousesQueryHandler(_warehouses.Object).Handle(new GetWarehousesQuery(), default);

        result.Should().ContainSingle().Which.Should().Be(new WarehouseDto(entrepot.Id, "Boutique", "Place d'Youville", false));
        _warehouses.Verify(w => w.ListAsync(1_000, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task EtantDonneStockReparti_QuandLireLaRepartition_AlorsUneLigneParEntrepotAvecLogistique()
    {
        var produit = Guid.NewGuid();
        var boutique = Stock.Create(produit, Guid.NewGuid(), 3);
        boutique.UpdateLogistics("B", "4", "P-2", 1, 3, null, null, null, null, "Vitrine");
        boutique.RecordInventoryTake(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), "Inès Morel");
        var reserve = Stock.Create(produit, Guid.NewGuid(), 40);
        var stocks = new Mock<IStockRepository>();
        stocks.Setup(s => s.ListByProductAsync(produit, It.IsAny<CancellationToken>())).ReturnsAsync([boutique, reserve]);

        var result = await new GetStockByProductQueryHandler(stocks.Object).Handle(new GetStockByProductQuery(produit), default);

        result.Should().HaveCount(2);
        var ligne = result.Single(s => s.WarehouseId == boutique.WarehouseId);
        ligne.Quantity.Should().Be(3);
        ligne.Section.Should().Be("B");
        ligne.Pallet.Should().Be("P-2");
        ligne.Comment.Should().Be("Vitrine");
        ligne.ResponsibleName.Should().Be("Inès Morel");
        result.Single(s => s.WarehouseId == reserve.WarehouseId).Quantity.Should().Be(40);
    }
}
