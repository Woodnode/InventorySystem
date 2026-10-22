using FluentAssertions;
using InventorySystem.Application.Common;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using InventorySystem.Application.Products.Queries;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Products;

/// <summary>Lectures du catalogue : pagination bornée, totaux de stock, emplacement principal, export.</summary>
public sealed class ProductQueriesTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStockRepository> _stocks = new();
    private readonly Mock<IExportService> _export = new();

    private static Product Produit(string sku, int seuil = 3, string nom = "Livre")
        => Product.Create(Sku.Create(sku), nom, null, seuil, collection: "Folio");

    private void Totaux(params (Product Produit, int Total)[] totaux)
        => _stocks.Setup(s => s.GetTotalQuantitiesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(totaux.ToDictionary(t => t.Produit.Id, t => t.Total));

    private void Page(params Product[] produits)
        => _products.Setup(p => p.ListPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<ProductSortBy>(), It.IsAny<bool>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((produits, produits.Length + 40));

    private void Emplacements(Dictionary<Guid, ProductLocation> lieux)
        => _stocks.Setup(s => s.GetPrimaryLocationsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lieux);

    // --- Liste paginée ---

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-4, 20, 1, 20)]
    [InlineData(3, 5000, 3, 100)]
    public async Task EtantDonnePaginationAberrante_QuandLister_AlorsBorneeAvantLaRequete(int page, int taille, int pageAttendue, int tailleAttendue)
    {
        Page();
        Totaux();
        Emplacements([]);

        var result = await new GetProductsQueryHandler(_products.Object, _stocks.Object)
            .Handle(new GetProductsQuery(page, taille), default);

        result.Page.Should().Be(pageAttendue);
        result.PageSize.Should().Be(tailleAttendue);
        _products.Verify(p => p.ListPagedAsync(pageAttendue, tailleAttendue, It.IsAny<string?>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<ProductSortBy>(), It.IsAny<bool>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task EtantDonneFiltres_QuandLister_AlorsTransmisTelsQuelsAuDepot()
    {
        Page();
        Totaux();
        Emplacements([]);

        await new GetProductsQueryHandler(_products.Object, _stocks.Object).Handle(
            new GetProductsQuery(2, 10, "prince", 1, 50, true, ProductSortBy.Quantity, true, "Folio"), default);

        _products.Verify(p => p.ListPagedAsync(2, 10, "prince", 1, 50, true, ProductSortBy.Quantity, true, "Folio",
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task EtantDonneProduitsDeLaPage_QuandLister_AlorsTotauxSeuilEtEmplacementCalcules()
    {
        var avecSection = Produit("LIV-0300", seuil: 5);
        var sansSection = Produit("LIV-0301", seuil: 5);
        var sansStock = Produit("LIV-0302", seuil: 0);
        var boutique = Guid.NewGuid();
        Page(avecSection, sansSection, sansStock);
        Totaux((avecSection, 4), (sansSection, 30));
        Emplacements(new()
        {
            [avecSection.Id] = new ProductLocation(boutique, "Boutique", "B", 4),
            [sansSection.Id] = new ProductLocation(boutique, "Boutique", "  ", 30),
        });

        var result = await new GetProductsQueryHandler(_products.Object, _stocks.Object).Handle(new GetProductsQuery(), default);

        result.TotalCount.Should().Be(43);
        var lignes = result.Items.ToDictionary(p => p.Sku);
        lignes["LIV-0300"].Quantity.Should().Be(4);
        lignes["LIV-0300"].IsLowOnStock.Should().BeTrue();
        lignes["LIV-0300"].PrimaryLocation.Should().Be("Boutique · B");
        lignes["LIV-0301"].IsLowOnStock.Should().BeFalse();
        lignes["LIV-0301"].PrimaryLocation.Should().Be("Boutique");
        lignes["LIV-0302"].Quantity.Should().Be(0);
        lignes["LIV-0302"].IsLowOnStock.Should().BeTrue("0 unité pour un seuil de 0 reste au seuil");
        lignes["LIV-0302"].PrimaryLocation.Should().BeNull();
    }

    // --- Fiche et scan ---

    [Fact]
    public async Task EtantDonneProduitInconnu_QuandLireParId_AlorsNullSansCalculerDeStock()
    {
        var result = await new GetProductByIdQueryHandler(_products.Object, _stocks.Object)
            .Handle(new GetProductByIdQuery(Guid.NewGuid()), default);

        result.Should().BeNull();
        _stocks.Verify(s => s.GetTotalQuantitiesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EtantDonneProduitConnu_QuandLireParId_AlorsFicheAvecStockTotal()
    {
        var produit = Produit("LIV-0303", seuil: 2, nom: "Vol de nuit");
        _products.Setup(p => p.GetByIdAsync(produit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(produit);
        Totaux((produit, 9));

        var result = await new GetProductByIdQueryHandler(_products.Object, _stocks.Object)
            .Handle(new GetProductByIdQuery(produit.Id), default);

        result!.Name.Should().Be("Vol de nuit");
        result.Quantity.Should().Be(9);
        result.IsLowOnStock.Should().BeFalse();
        result.Collection.Should().Be("Folio");
    }

    // --- Stock bas, collections ---

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(2, 500, 2, 100)]
    public async Task EtantDonnePaginationAberrante_QuandListerLeStockBas_AlorsBornee(int page, int taille, int pageAttendue, int tailleAttendue)
    {
        _products.Setup(p => p.ListLowStockPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<(Product, int)>(), 0));

        var result = await new GetLowStockQueryHandler(_products.Object).Handle(new GetLowStockQuery(page, taille), default);

        result.Page.Should().Be(pageAttendue);
        result.PageSize.Should().Be(tailleAttendue);
    }

    [Fact]
    public async Task EtantDonneProduitsSousLeSeuil_QuandListerLeStockBas_AlorsTousMarquesBas()
    {
        var produit = Produit("LIV-0304", seuil: 5);
        _products.Setup(p => p.ListLowStockPagedAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { (produit, 1) }, 1));

        var result = await new GetLowStockQueryHandler(_products.Object).Handle(new GetLowStockQuery(), default);

        result.Items.Should().ContainSingle().Which.Should().Match<ProductDto>(p => p.IsLowOnStock && p.Quantity == 1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(10_000, 200)]
    public async Task EtantDonneNombreDeCollectionsDemande_QuandLister_AlorsBorneEntre1Et200(int demande, int attendu)
    {
        _products.Setup(p => p.CountByCollectionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CollectionCount("Folio", 12)]);

        var result = await new GetCollectionsQueryHandler(_products.Object).Handle(new GetCollectionsQuery(demande), default);

        _products.Verify(p => p.CountByCollectionAsync(attendu, It.IsAny<CancellationToken>()));
        result.Should().ContainSingle().Which.Should().Be(new CollectionDto("Folio", 12));
    }

    // --- Export ---

    private void ExportRenvoie(string nom)
        => _export.Setup(e => e.ExportProductsAsync(It.IsAny<IReadOnlyList<ProductDto>>(), It.IsAny<ExportFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportFileDto(new MemoryStream(), "text/csv", nom));

    [Fact]
    public async Task EtantDonneCatalogueSousLaLimite_QuandExporter_AlorsFichierCompletNonTronque()
    {
        var produit = Produit("LIV-0305");
        _products.Setup(p => p.ListAsync(ExportLimits.MaxRows, It.IsAny<CancellationToken>())).ReturnsAsync([produit]);
        Totaux((produit, 2));
        ExportRenvoie("produits.csv");

        var file = await new ExportProductsQueryHandler(_products.Object, _stocks.Object, _export.Object)
            .Handle(new ExportProductsQuery(ExportFormat.Csv), default);

        file.Truncated.Should().BeFalse();
        file.FileName.Should().Be("produits.csv");
        _export.Verify(e => e.ExportProductsAsync(
            It.Is<IReadOnlyList<ProductDto>>(l => l.Single().Quantity == 2 && l.Single().IsLowOnStock), ExportFormat.Csv,
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task EtantDonneCatalogueAuPlafond_QuandExporter_AlorsFichierMarqueTronque()
    {
        var produits = Enumerable.Range(0, ExportLimits.MaxRows).Select(i => Produit($"LIV-{i:00000}")).ToList();
        _products.Setup(p => p.ListAsync(ExportLimits.MaxRows, It.IsAny<CancellationToken>())).ReturnsAsync(produits);
        Totaux();
        ExportRenvoie("produits.xlsx");

        var file = await new ExportProductsQueryHandler(_products.Object, _stocks.Object, _export.Object)
            .Handle(new ExportProductsQuery(ExportFormat.Excel), default);

        file.Truncated.Should().BeTrue();
        file.FileName.Should().Be("produits_tronque.xlsx");
    }
}
