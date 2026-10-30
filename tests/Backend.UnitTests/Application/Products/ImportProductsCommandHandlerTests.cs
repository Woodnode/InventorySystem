using System.Text;
using FluentAssertions;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Commands;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using MiniExcelLibs;
using Moq;

namespace InventorySystem.Backend.UnitTests.Application.Products;

/// <summary>
/// Import du catalogue (CSV et Excel) : une ligne invalide est écartée avec un message, sans
/// empêcher les autres d'entrer ; un produit déjà connu est mis à jour sans effacer ses champs
/// quand la ligne est incomplète ; l'enregistrement se fait en une seule fois à la fin.
/// </summary>
public sealed class ImportProductsCommandHandlerTests
{
    private readonly FauxProduits _produits = new();
    private readonly FauxStocks _stocks = new();
    private readonly FauxEntrepots _entrepots = new();
    private readonly FausseUniteDeTravail _uniteDeTravail = new();

    private Task<ImportResult> ImporterCsvAsync(string csv, string nomFichier = "catalogue.csv")
        => new ImportProductsCommandHandler(_produits, _stocks, _entrepots, _uniteDeTravail, NullLogger<ImportProductsCommandHandler>.Instance)
            .Handle(new ImportProductsCommand(new MemoryStream(Encoding.UTF8.GetBytes(csv)), nomFichier), default);

    private Task<ImportResult> ImporterExcelAsync(object feuilles, string nomFichier = "catalogue.xlsx")
    {
        var flux = new MemoryStream();
        flux.SaveAs(feuilles);
        flux.Position = 0;
        return new ImportProductsCommandHandler(_produits, _stocks, _entrepots, _uniteDeTravail, NullLogger<ImportProductsCommandHandler>.Instance)
            .Handle(new ImportProductsCommand(flux, nomFichier), default);
    }

    private Product Ajoute(string sku) => _produits.Ajoutes.Single(p => p.Sku.Value == sku);

    private Warehouse Entrepot(string nom) => _entrepots.Ajoutes.Concat(_entrepots.Existants).Single(w => w.Name == nom);

    // --- CSV ---

    [Fact]
    public async Task EtantDonneCsvValide_QuandImporter_AlorsProduitsEtStocksCreesEnUnSeulEnregistrement()
    {
        var result = await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre Copies
            LIV-0001;Le Petit Prince;Entrepôt central;12
            LIV-0002;Vol de nuit;Entrepôt central;4
            """);

        result.ProductsCreated.Should().Be(2);
        result.ProductsUpdated.Should().Be(0);
        result.Errors.Should().BeEmpty();
        _entrepots.Ajoutes.Should().ContainSingle().Which.Name.Should().Be("Entrepôt central");
        _stocks.Lignes.Select(s => s.Quantity).Should().BeEquivalentTo([12, 4]);
        _uniteDeTravail.Enregistrements.Should().Be(1);
    }

    [Fact]
    public async Task EtantDonneCsvSepareParDesVirgules_QuandImporter_AlorsSeparateurDetecte()
    {
        var result = await ImporterCsvAsync("""
            Code produit,Titre,Collection
            LIV-0003,Terre des hommes,Folio
            """);

        result.ProductsCreated.Should().Be(1);
        Ajoute("LIV-0003").Collection.Should().Be("Folio");
    }

    [Fact]
    public async Task EtantDonneNouveauProduitSansTitre_QuandImporter_AlorsLigneEcarteeEtLesAutresImportees()
    {
        var result = await ImporterCsvAsync("""
            Code produit;Titre
            LIV-0010;
            LIV-0011;Citadelle
            """);

        result.ProductsCreated.Should().Be(1);
        result.Errors.Should().ContainSingle().Which.Should().Be("Le produit LIV-0010 n'a pas de titre, ignoré.");
        _produits.Ajoutes.Should().ContainSingle(p => p.Sku.Value == "LIV-0011");
    }

    [Fact]
    public async Task EtantDonneLigneSansCodeProduit_QuandImporter_AlorsIgnoreeSansErreur()
    {
        var result = await ImporterCsvAsync("""
            Code produit;Titre
            ;Titre orphelin
            LIV-0012;Pilote de guerre
            """);

        result.ProductsCreated.Should().Be(1);
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("CODE-BEAUCOUP-TROP-LONG-POUR-UN-SKU-VALIDE")]
    public async Task EtantDonneCodeProduitHorsBornes_QuandImporter_AlorsLigneEcarteeAvecLeMotif(string code)
    {
        var result = await ImporterCsvAsync($"""
            Code produit;Titre
            {code};Livre au code invalide
            LIV-0013;Courrier sud
            """);

        result.ProductsCreated.Should().Be(1);
        result.Errors.Should().ContainSingle().Which.Should()
            .Be($"Le produit {code} a été ignoré: Le SKU doit contenir entre 3 et 32 caractères.");
    }

    [Fact]
    public async Task EtantDonneMemeProduitADeuxEmplacements_QuandImporter_AlorsUnProduitEtDeuxStocksQuantifies()
    {
        var result = await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre Copies
            LIV-0020;Le Petit Prince;Boutique;3
            LIV-0020;Le Petit Prince;Entrepôt;9
            """);

        result.ProductsCreated.Should().Be(1);
        result.ProductsUpdated.Should().Be(1, "la seconde ligne du même SKU complète le produit créé plus haut");
        _produits.Ajoutes.Should().ContainSingle();
        _stocks.Lignes.Should().HaveCount(2);
        _stocks.Lignes.Single(s => s.WarehouseId == Entrepot("Boutique").Id).Quantity.Should().Be(3);
        _stocks.Lignes.Single(s => s.WarehouseId == Entrepot("Entrepôt").Id).Quantity.Should().Be(9);
    }

    [Fact]
    public async Task EtantDonneMemeProduitDeuxFoisAuMemeEmplacement_QuandImporter_AlorsUneSeuleLigneDeStock()
    {
        // La quantité initiale ne s'applique qu'à la création de la ligne : un doublon de
        // ligne dans le fichier ne double pas le stock.
        await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre Copies
            LIV-0021;Vol de nuit;Boutique;5
            LIV-0021;Vol de nuit;boutique;5
            """);

        _entrepots.Ajoutes.Should().ContainSingle("le nom d'entrepôt est comparé sans tenir compte de la casse");
        _stocks.Lignes.Should().ContainSingle().Which.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task EtantDonneEntrepotExistant_QuandImporter_AlorsRattacheSansEnCreerUnAutre()
    {
        var central = Warehouse.Create("Entrepôt central", "1 rue du Port");
        _entrepots.Existants.Add(central);

        await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre Copies
            LIV-0022;Terre des hommes;ENTREPÔT CENTRAL;2
            """);

        _entrepots.Ajoutes.Should().BeEmpty();
        _stocks.Lignes.Should().ContainSingle().Which.WarehouseId.Should().Be(central.Id);
    }

    [Fact]
    public async Task EtantDonneProduitDejaConnu_QuandReimporterUneLigneIncomplete_AlorsChampsVidesConserves()
    {
        var existant = Product.Create(Sku.Create("LIV-0030"), "Ancien titre", "Description d'origine", 4,
            collection: "Folio", productType: "Roman", year: 1943, company: "Gallimard");
        _produits.Enregistres.Add(existant);

        var result = await ImporterCsvAsync("""
            Code produit;Titre;Description;Collection;Type;Année;Compagnies
            LIV-0030;Nouveau titre;;Folio junior;;;
            """);

        result.ProductsCreated.Should().Be(0);
        result.ProductsUpdated.Should().Be(1);
        existant.Name.Should().Be("Nouveau titre");
        existant.Collection.Should().Be("Folio junior");
        existant.Description.Should().Be("Description d'origine");
        existant.ProductType.Should().Be("Roman");
        existant.Year.Should().Be(1943);
        existant.Company.Should().Be("Gallimard");
        existant.LowStockThreshold.Should().Be(4, "l'import ne touche pas au seuil d'un produit existant");
    }

    [Fact]
    public async Task EtantDonneStockExistant_QuandReimporter_AlorsQuantiteInchangeeMaisEmplacementMisAJour()
    {
        // La quantité vit des mouvements : un réimport ne doit pas l'écraser ni l'additionner.
        var produit = Product.Create(Sku.Create("LIV-0031"), "Citadelle", null, 2);
        _produits.Enregistres.Add(produit);
        var boutique = Warehouse.Create("Boutique", null);
        _entrepots.Existants.Add(boutique);
        var stock = Stock.Create(produit.Id, boutique.Id, 8);
        _stocks.Lignes.Add(stock);

        await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre Copies;Section;Espace
            LIV-0031;Citadelle;Boutique;50;B;3
            """);

        stock.Quantity.Should().Be(8);
        stock.Section.Should().Be("B");
        stock.Space.Should().Be("3");
    }

    [Fact]
    public async Task EtantDonneZeroCopie_QuandImporter_AlorsLigneDeStockAZeroSansErreur()
    {
        var result = await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre Copies
            LIV-0040;Le Petit Prince;Réserve;0
            """);

        result.Errors.Should().BeEmpty();
        _stocks.Lignes.Should().ContainSingle().Which.Quantity.Should().Be(0);
    }

    [Fact]
    public async Task EtantDonnePoidsEnLivres_QuandImporter_AlorsConvertiEnGrammes()
    {
        await ImporterCsvAsync("""
            Code produit;Titre;Poids / copie (lb)
            LIV-0041;Vol de nuit;1
            """);

        Ajoute("LIV-0041").WeightPerCopyGrams.Should().Be(453.59237m);
    }

    [Fact]
    public async Task EtantDonnePoidsEnLivresEtEnGrammes_QuandImporter_AlorsLesGrammesPriment()
    {
        await ImporterCsvAsync("""
            Code produit;Titre;Poids / copie (lb);Poids / copie (g)
            LIV-0042;Vol de nuit;1;250
            """);

        Ajoute("LIV-0042").WeightPerCopyGrams.Should().Be(250m);
    }

    [Fact]
    public async Task EtantDonneDatesDeLogistique_QuandImporter_AlorsLuesEnUtcEtDateInvalideIgnoree()
    {
        await ImporterCsvAsync("""
            Code produit;Titre;Location;CAF - Date Entrée;CAF - Date Sortie;Nom du distributeur
            LIV-0043;Courrier sud;CAF;2026-03-14;pas une date;Diffusion Nord
            """);

        var stock = _stocks.Lignes.Single();
        stock.EntryDate.Should().Be(new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));
        stock.EntryDate!.Value.Kind.Should().Be(DateTimeKind.Utc, "PostgreSQL refuse un horodatage sans fuseau");
        stock.ExitDate.Should().BeNull();
        stock.DistributorName.Should().Be("Diffusion Nord");
    }

    [Fact]
    public async Task EtantDonnePriseDInventaire_QuandImporter_AlorsDateEtResponsableEnregistres()
    {
        await ImporterCsvAsync("""
            Code produit;Titre;Location;Date de prise inventaire;Nom du responsable
            LIV-0044;Citadelle;Boutique;2026-01-31;Inès Morel
            """);

        var stock = _stocks.Lignes.Single();
        stock.InventoryDate.Should().Be(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));
        stock.ResponsibleName.Should().Be("Inès Morel");
    }

    [Fact]
    public async Task EtantDonneLigneSansEmplacement_QuandImporter_AlorsProduitSansStock()
    {
        await ImporterCsvAsync("""
            Code produit;Titre;Nbre Copies
            LIV-0045;Terre des hommes;7
            """);

        _produits.Ajoutes.Should().ContainSingle();
        _stocks.Lignes.Should().BeEmpty();
    }

    [Fact]
    public async Task EtantDonneNouveauProduit_QuandImporter_AlorsSeuilParDefautDeDix()
    {
        await ImporterCsvAsync("""
            Code produit;Titre
            liv-0046;Pilote de guerre
            """);

        var produit = _produits.Ajoutes.Single();
        produit.LowStockThreshold.Should().Be(10);
        produit.Sku.Value.Should().Be("LIV-0046", "le SKU est normalisé en majuscules");
    }

    [Fact]
    public async Task EtantDonneNombreDeBoitesNegatif_QuandImporter_AlorsRienNEstCreePourCetteLigne()
    {
        var result = await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre de boîtes
            LIV-0047;Vol de nuit;Boutique;-1
            """);

        result.Errors.Should().ContainSingle();
        result.ProductsCreated.Should().Be(0);
        _produits.Ajoutes.Should().BeEmpty();
        _stocks.Lignes.Should().BeEmpty();
    }

    [Fact]
    public async Task EtantDonneEntrepotDesactive_QuandImporterUneLigneVersLui_AlorsLigneEcarteeSansRienCreer()
    {
        var ferme = Warehouse.Create("Ancien dépôt", null);
        ferme.Deactivate();
        _entrepots.Existants.Add(ferme);

        var result = await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre Copies
            LIV-0050;Vol de nuit;ancien dépôt;4
            LIV-0051;Citadelle;Boutique;2
            """);

        result.Errors.Should().ContainSingle().Which.Should()
            .Be("Le produit LIV-0050 a été ignoré: L'entrepôt « Ancien dépôt » est désactivé.");
        result.ProductsCreated.Should().Be(1);
        _produits.Ajoutes.Should().ContainSingle(p => p.Sku.Value == "LIV-0051");
        _stocks.Lignes.Should().NotContain(l => l.WarehouseId == ferme.Id);
    }

    [Fact]
    public async Task EtantDonneNombreDeBoitesNegatif_QuandImporter_AlorsErreurMetierRemonteeEtImportPoursuivi()
    {
        var result = await ImporterCsvAsync("""
            Code produit;Titre;Location;Nbre de boîtes
            LIV-0048;Vol de nuit;Boutique;-1
            LIV-0049;Citadelle;Boutique;2
            """);

        result.Errors.Should().ContainSingle().Which.Should()
            .Be("Le produit LIV-0048 a été ignoré: Le nombre de boîtes ne peut pas être négatif.");
        _stocks.Lignes.Should().Contain(s => s.BoxesCount == 2);
        _uniteDeTravail.Enregistrements.Should().Be(1);
    }

    // --- Excel ---

    private static Dictionary<string, object> Ligne(params (string Colonne, object Valeur)[] cellules)
        => cellules.ToDictionary(c => c.Colonne, c => c.Valeur);

    [Fact]
    public async Task EtantDonneClasseurAUneFeuille_QuandImporter_AlorsPremiereFeuilleLue()
    {
        var lignes = new List<Dictionary<string, object>>
        {
            Ligne(("Code produit", "LIV-0100"), ("Titre", "Le Petit Prince"), ("Localisation", "Boutique"), ("Quantité", 6)),
        };

        var result = await ImporterExcelAsync(lignes);

        result.ProductsCreated.Should().Be(1);
        _stocks.Lignes.Single().Quantity.Should().Be(6);
    }

    [Fact]
    public async Task EtantDonneClasseurAPlusieursFeuilles_QuandImporter_AlorsFeuilleInventaireCiblee()
    {
        // La première feuille du vrai classeur a d'autres colonnes : la lire n'importerait rien.
        var feuilles = new Dictionary<string, object>
        {
            ["Données Manuelles"] = new List<Dictionary<string, object>> { Ligne(("Code PP", "X"), ("Sujet", "Y")) },
            ["Inventaire"] = new List<Dictionary<string, object>>
            {
                Ligne(("Code produit", "LIV-0101"), ("Titre", "Vol de nuit"), ("Compagnies", "Gallimard")),
            },
        };

        var result = await ImporterExcelAsync(feuilles);

        result.ProductsCreated.Should().Be(1);
        Ajoute("LIV-0101").Company.Should().Be("Gallimard");
    }

    [Fact]
    public async Task EtantDonneClasseurAvecMacros_QuandImporter_AlorsLuCommeUnClasseurExcel()
    {
        var lignes = new List<Dictionary<string, object>> { Ligne(("Code produit", "LIV-0102"), ("Titre", "Citadelle")) };

        var result = await ImporterExcelAsync(lignes, "catalogue.xlsm");

        result.ProductsCreated.Should().Be(1);
    }

    [Fact]
    public async Task EtantDonneTitreDePlusDe200Caracteres_QuandImporterDepuisExcel_AlorsTronqueSansFaireEchouerLImport()
    {
        var lignes = new List<Dictionary<string, object>>
        {
            Ligne(("Code produit", "LIV-0103"), ("Titre", new string('T', 260)), ("Commentaire", new string('c', 1500))),
        };

        var result = await ImporterExcelAsync(lignes);

        result.Errors.Should().BeEmpty();
        Ajoute("LIV-0103").Name.Should().HaveLength(200);
    }

    [Fact]
    public async Task EtantDonneFichierXlsxQuiNEstPasUnClasseur_QuandImporter_AlorsMessageClairSansEcriture()
    {
        var result = await new ImportProductsCommandHandler(_produits, _stocks, _entrepots, _uniteDeTravail, NullLogger<ImportProductsCommandHandler>.Instance)
            .Handle(new ImportProductsCommand(new MemoryStream(Encoding.UTF8.GetBytes("ceci n'est pas un zip")), "faux.xlsx"), default);

        result.Errors.Should().ContainSingle().Which.Should().Be("Le fichier n'est pas un classeur Excel valide.");
        _uniteDeTravail.Enregistrements.Should().Be(0);
    }

    [Fact]
    public async Task EtantDonneEntetesAlternatifsDansExcel_QuandImporter_AlorsReconnus()
    {
        var lignes = new List<Dictionary<string, object>>
        {
            Ligne(("SKU", "LIV-0104"), ("Nom", "Terre des hommes"), ("Entrepôt", "Réserve"), ("Qty", 11), ("Year", 1939)),
        };

        await ImporterExcelAsync(lignes);

        var produit = Ajoute("LIV-0104");
        produit.Name.Should().Be("Terre des hommes");
        produit.Year.Should().Be(1939);
        _stocks.Lignes.Single().Quantity.Should().Be(11);
    }
}

/// <summary>
/// Non-régression de l'audit de sécurité sur l'import : plafond de lignes, troncature de la
/// Location (nom d'entrepôt) et absence de message interne de bibliothèque renvoyé au client.
/// </summary>
public sealed class ImportProductsCommandHandlerAuditTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStockRepository> _stocks = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ImportProductsCommandHandlerAuditTests()
    {
        _warehouses
            .Setup(w => w.ListAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Warehouse>());
        _stocks
            .Setup(s => s.GetOrCreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid productId, Guid warehouseId, CancellationToken _) => Stock.Create(productId, warehouseId));
    }

    private ImportProductsCommandHandler CreateHandler()
        => new(_products.Object, _stocks.Object, _warehouses.Object, _unitOfWork.Object,
            NullLogger<ImportProductsCommandHandler>.Instance);

    private static MemoryStream Csv(string content) => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Handle_CsvAboveRowLimit_RejectsWithoutPersisting()
    {
        var builder = new StringBuilder("Code produit;Titre\n");
        for (var i = 0; i <= ImportLimits.MaxRows; i++)
            builder.Append("SKU-").Append(i).Append(";Titre\n");

        var result = await CreateHandler().Handle(
            new ImportProductsCommand(Csv(builder.ToString()), "import.csv"), CancellationToken.None);

        result.ProductsCreated.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Contain("Trop de lignes");
        _products.Verify(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CsvWithOverlongLocation_TruncatesWarehouseName()
    {
        Warehouse? created = null;
        _warehouses
            .Setup(w => w.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()))
            .Callback((Warehouse w, CancellationToken _) => created = w)
            .Returns(Task.CompletedTask);

        var csv = "Code produit;Titre;Location\nSKU-1;Titre;" + new string('A', 5_000) + "\n";

        await CreateHandler().Handle(new ImportProductsCommand(Csv(csv), "import.csv"), CancellationToken.None);

        created.Should().NotBeNull();
        created!.Name.Length.Should().Be(200);
    }

    [Fact]
    public async Task Handle_UnreadableExcel_ReturnsGenericMessage()
    {
        // Un faux classeur fait lever une exception (archive zip illisible) : son message
        // (chemins, détails internes) ne doit pas remonter tel quel jusqu'au client.
        var garbage = new MemoryStream(Encoding.UTF8.GetBytes("ceci n'est pas un classeur"));

        var result = await CreateHandler().Handle(
            new ImportProductsCommand(garbage, "import.xlsx"), CancellationToken.None);

        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Le fichier n'est pas un classeur Excel valide.");
    }
}
