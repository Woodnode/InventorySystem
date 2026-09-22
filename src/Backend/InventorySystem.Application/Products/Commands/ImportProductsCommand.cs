using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Application.Common.Interfaces;
using MediatR;
using MiniExcelLibs;
using DocumentFormat.OpenXml.Spreadsheet;
using CsvHelper.Configuration.Attributes;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Application.Products.Commands;

public sealed record ImportProductsCommand(Stream FileStream, string FileName) : IRequest<ImportResult>;

public sealed record ImportResult(int ProductsCreated, int ProductsUpdated, List<string> Errors);

public sealed class CsvProductRecord
{
    [Name("Code produit")]
    public string? CodeProduit { get; set; }

    [Name("Titre")]
    public string? Titre { get; set; }

    [Name("Description")]
    public string? Description { get; set; }

    [Name("Location")]
    public string? Location { get; set; }

    [Name("Code projet")]
    public string? CodeProjet { get; set; }

    [Name("Collection")]
    public string? Collection { get; set; }

    [Name("Vol., №", "Vol., Nº", "Vol., No", "Vol., N°")]
    public string? VolNo { get; set; }

    [Name("Type")]
    public string? Type { get; set; }

    [Name("Année")]
    public int? Annee { get; set; }

    [Name("Compagnies", "Compagnie")]
    public string? Compagnies { get; set; }

    [Name("Section")]
    public string? Section { get; set; }

    [Name("Espace")]
    public string? Espace { get; set; }

    [Name("Palette CAF")]
    public string? PaletteCAF { get; set; }

    [Name("Nbre de boîtes", "Nbre de boites")]
    public int? NbreDeBoites { get; set; }

    [Name("Nbre copies/boîte", "Nbre copies/boite")]
    public int? NbreCopiesBoite { get; set; }

    [Name("Nbre Copies", "Total Copies", "Nbre copies")]
    public int? NbreCopies { get; set; }

    [Name("Poids / copie (lb)", "Poids/copie (lb)")]
    public decimal? PoidsCopieLb { get; set; }

    [Name("CAF - Date Entrée", "CAF - Date Entree")]
    public string? DateEntree { get; set; }

    [Name("CAF - Date Sortie")]
    public string? DateSortie { get; set; }

    [Name("Nom du distributeur")]
    public string? NomDistributeur { get; set; }

    [Name("Date du retour")]
    public string? DateRetour { get; set; }

    [Name("Commentaire")]
    public string? Commentaire { get; set; }

    // Prise d'inventaire physique — présents sur la même ligne que le reste dans le
    // classeur réel (feuille "Inventaire"), pas sur une feuille séparée à recouper.
    [Name("Date de prise inventaire", "Date de prise d'inventaire", "Date d'inventaire")]
    public string? DatePriseInventaire { get; set; }

    [Name("Nom du responsable", "Responsable")]
    public string? NomResponsable { get; set; }
}

public sealed class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, ImportResult>
{
    private readonly IProductRepository _products;
    private readonly IStockRepository _stocks;
    private readonly IWarehouseRepository _warehouses;
    private readonly IUnitOfWork _unitOfWork;

    public ImportProductsCommandHandler(
        IProductRepository products,
        IStockRepository stocks,
        IWarehouseRepository warehouses,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _stocks = stocks;
        _warehouses = warehouses;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResult> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        var records = new List<CsvProductRecord>();

        static string? GetVal(IDictionary<string, object> rowDict, params string[] names)
        {
            foreach (var n in names)
            {
                // Recherche insensible à la casse
                var key = rowDict.Keys.FirstOrDefault(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    var cellVal = rowDict[key]?.ToString();
                    if (!string.IsNullOrWhiteSpace(cellVal))
                    {
                        return cellVal.Trim();
                    }
                }
            }
            return null;
        }

        static int? GetInt(IDictionary<string, object> rowDict, params string[] names)
        {
            var v = GetVal(rowDict, names);
            if (int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out int r)) return r;
            if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)) return (int)d;
            return null;
        }

        static decimal? GetDec(IDictionary<string, object> rowDict, params string[] names)
        {
            var v = GetVal(rowDict, names);
            if (decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal r)) return r;
            if (decimal.TryParse(v, out decimal r2)) return r2;
            return null;
        }

        // Un fichier Excel réel contient des anomalies de saisie (cellule collée depuis un
        // autre document, formule mal recopiée...) qui peuvent dépasser la longueur de colonne
        // attendue. Sans cette garde, UNE cellule trop longue faisait échouer SaveChanges (donc
        // TOUT l'import, plus aucune ligne traitée) avec une DbUpdateException PostgreSQL peu
        // parlante ("value too long for type character varying(n)") plutôt qu'un simple
        // tronquage silencieux de cette valeur précise.
        static string? Truncate(string? value, int maxLength) =>
            value is not null && value.Length > maxLength ? value[..maxLength] : value;

        try
        {
            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            if (extension == ".xlsx" || extension == ".xls" || extension == ".xlsm")
            {
                using var ms = new MemoryStream();
                await request.FileStream.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;

                // MiniExcelLibs.ExcelType (v1.45.0) ne connaît que XLSX/CSV/UNKNOWN : pas de valeur XLS
                // dédiée. .xlsm est un classeur OOXML/zip identique à .xlsx (juste avec des macros), donc
                // ExcelType.XLSX le lit correctement. Sans ce paramètre explicite, un .xlsm tombait
                // auparavant dans la branche CSV ci-dessous et CsvHelper tentait de lire le binaire ZIP
                // du classeur comme du texte, ce qui plantait tout le processus (crash silencieux, sans
                // exception .NET catchable).
                try
                {
                    List<string> sheetNames;
                    try
                    {
                        ms.Position = 0;
                        sheetNames = MiniExcel.GetSheetNames(ms).ToList();
                    }
                    catch
                    {
                        sheetNames = new List<string>();
                    }

                    // Le classeur d'inventaire réel (ex. "00_Master Inventaire") contient plusieurs
                    // onglets : "Données Manuelles" et "ListeProduits" ont un schéma de colonnes
                    // différent et incompatible (ex. "Code PP"/"Sujet" au lieu de "Code produit"/
                    // "Titre" — aucune ligne ne s'y reconnaîtrait). La feuille "Inventaire" est celle
                    // qui porte réellement Code produit, Titre, Collection, Vol. N°, Type, Compagnies,
                    // Année, Location, Section, Espace ET Date de prise inventaire/Nom du responsable
                    // sur une même ligne. Sans préciser sheetName, MiniExcel.Query lit le premier
                    // onglet du classeur par position (souvent "Données Manuelles"), ce qui explique
                    // des champs absents ou visiblement erronés même sur un fichier bien rempli. On
                    // cible donc "Inventaire" par nom quand il existe, et on ne retombe sur le
                    // comportement "premier onglet" que pour un classeur à un seul onglet (fichier
                    // simple sans cet onglet nommé).
                    var mainSheetName = sheetNames.FirstOrDefault(n =>
                        string.Equals(n, "Inventaire", StringComparison.OrdinalIgnoreCase));

                    ms.Position = 0;
                    var rows = mainSheetName != null
                        ? MiniExcel.Query(ms, useHeaderRow: true, sheetName: mainSheetName, excelType: ExcelType.XLSX).ToList()
                        : MiniExcel.Query(ms, useHeaderRow: true, excelType: ExcelType.XLSX).ToList();

                    foreach (var row in rows)
                    {
                        var rowDict = (IDictionary<string, object>)row;

                        var r = new CsvProductRecord
                        {
                            CodeProduit = GetVal(rowDict, "Item", "SKU", "CodeProduit", "Code", "Code produit"),
                            Titre = Truncate(GetVal(rowDict, "Titre", "Name", "Nom"), 200),
                            Description = Truncate(GetVal(rowDict, "Description"), 1000),
                            Compagnies = Truncate(GetVal(rowDict, "Compagnies", "Compagnie"), 150),
                            CodeProjet = Truncate(GetVal(rowDict, "Code de projet", "Projet", "Code projet"), 200),
                            Collection = Truncate(GetVal(rowDict, "Collection"), 150),
                            VolNo = Truncate(GetVal(rowDict, "Vol., №", "Vol., Nº", "Vol., No", "Vol., N°", "Vol. N°", "Vol N°", "Volume"), 50),
                            Type = Truncate(GetVal(rowDict, "Type"), 100),
                            Annee = GetInt(rowDict, "Année", "Annee", "Year"),
                            PoidsCopieLb = GetDec(rowDict, "Poids/copie (lb)", "Poids", "Poids / copie (lb)"),
                            Location = GetVal(rowDict, "Localisation", "Location", "Entrepôt"),
                            Section = Truncate(GetVal(rowDict, "Section"), 50),
                            Espace = Truncate(GetVal(rowDict, "Espace"), 50),
                            PaletteCAF = Truncate(GetVal(rowDict, "Palette CAF", "Palette"), 50),
                            NbreDeBoites = GetInt(rowDict, "Nbre de boîtes", "Boites", "Nbre de boites"),
                            NbreCopiesBoite = GetInt(rowDict, "Nbre copies/boîte", "Copies par boite", "Nbre copies/boite"),
                            NbreCopies = GetInt(rowDict, "Nbre copies", "Quantité", "Qty", "Nbre Copies", "Total Copies"),
                            DateEntree = GetVal(rowDict, "Date d'entrée", "Date Entree", "CAF - Date Entrée", "CAF - Date Entree"),
                            DateSortie = GetVal(rowDict, "Date de sortie", "Date Sortie", "CAF - Date Sortie"),
                            NomDistributeur = Truncate(GetVal(rowDict, "Nom du distributeur", "Distributeur"), 150),
                            DateRetour = GetVal(rowDict, "Date de retour", "Date Retour", "Date du retour"),
                            Commentaire = Truncate(GetVal(rowDict, "Commentaire", "Notes"), 1000),
                            DatePriseInventaire = GetVal(rowDict, "Date de prise inventaire", "Date de prise d'inventaire", "Date d'inventaire"),
                            NomResponsable = Truncate(GetVal(rowDict, "Nom du responsable", "Responsable"), 150)
                        };

                        if (!string.IsNullOrWhiteSpace(r.CodeProduit) || !string.IsNullOrWhiteSpace(r.Titre))
                        {
                            records.Add(r);
                        }
                    }
                }
                catch (Exception ex)
                {
                    return new ImportResult(0, 0, new List<string> { "Erreur de lecture du fichier Excel: " + ex.Message });
                }
            }
            else
            {
                var config = new CsvConfiguration(CultureInfo.GetCultureInfo("fr-FR"))
                {
                    Delimiter = ";",
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null,
                    BadDataFound = null,
                    IgnoreBlankLines = true
                };

                using var reader = new StreamReader(request.FileStream);
                var firstLine = await reader.ReadLineAsync();
                if (firstLine != null && firstLine.Contains(',') && !firstLine.Contains(';'))
                {
                    config.Delimiter = ",";
                }
                request.FileStream.Position = 0;
                reader.DiscardBufferedData();

                using var csv = new CsvReader(reader, config);
                records = csv.GetRecords<CsvProductRecord>().ToList();
            }
        }
        catch (Exception ex)
        {
            return new ImportResult(0, 0, new List<string> { $"Erreur de lecture du fichier: {ex.Message}" });
        }

        var allWarehouses = (await _warehouses.ListAsync(1000, cancellationToken)).ToList();

        int created = 0;
        int updated = 0;
        var errors = new List<string>();

        // Un même SKU peut apparaître sur plusieurs lignes du fichier (ex: même produit à
        // plusieurs emplacements/volumes). SaveChanges n'est appelé qu'une seule fois à la fin
        // (voir plus bas), donc _products.GetBySkuAsync ne verrait pas un produit créé plus tôt
        // dans ce même lot (pas encore en base) : sans ce cache, deux lignes du même SKU
        // créaient chacune un nouveau Product, et la contrainte d'unicité en base faisait
        // échouer tout l'import (ConcurrencyConflictException "Ce SKU est déjà utilisé...").
        var productsInBatch = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);

        // Même principe que productsInBatch, mais par (Produit, Entrepôt) : un même produit
        // apparaît typiquement sur plusieurs lignes du fichier "Inventaire" (une par
        // emplacement — Boutique, Entrepôt, CAF...). Sans ce cache, une ligne de stock déjà
        // ajoutée dans CE lot mais pas encore sauvegardée (SaveChanges n'est appelé qu'à la
        // fin) ne serait pas retrouvée par une requête SQL fraîche, et créerait une seconde
        // ligne de stock en doublon pour le même (Produit, Entrepôt) — violation de l'index
        // unique sur SaveChanges.
        var stocksInBatch = new Dictionary<(Guid ProductId, Guid WarehouseId), Stock>();

        static DateTime? ParseDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;

            // Les colonnes stocks.*Date sont en "timestamp with time zone" : Npgsql refuse
            // d'écrire un DateTime.Kind=Unspecified (celui que DateTime.TryParse renvoie par
            // défaut) sur ce type de colonne — "only UTC is supported". Les dates du fichier
            // n'ont pas de fuseau explicite, on les traite donc comme UTC "au sens large" (le
            // jour du calendrier compte, pas l'heure précise).
            if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return DateTime.SpecifyKind(d, DateTimeKind.Utc);
            if (DateTime.TryParse(dateStr, out var d2))
                return DateTime.SpecifyKind(d2, DateTimeKind.Utc);
            return null;
        }

        static string? Coalesce(string? newVal, string? existing) =>
            string.IsNullOrWhiteSpace(newVal) ? existing : newVal;

        async Task<Warehouse> GetOrCreateWarehouseAsync(string name)
        {
            var warehouse = allWarehouses.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (warehouse == null)
            {
                warehouse = Warehouse.Create(name, null);
                await _warehouses.AddAsync(warehouse, cancellationToken);
                allWarehouses.Add(warehouse);
            }
            return warehouse;
        }

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.CodeProduit)) continue;

            try
            {
                var product = productsInBatch.GetValueOrDefault(record.CodeProduit)
                    ?? await _products.GetBySkuAsync(record.CodeProduit, cancellationToken);

                if (product == null)
                {
                    if (string.IsNullOrWhiteSpace(record.Titre))
                    {
                        errors.Add($"Le produit {record.CodeProduit} n'a pas de titre, ignoré.");
                        continue;
                    }

                    product = Product.Create(
                        InventorySystem.Domain.ValueObjects.Sku.Create(record.CodeProduit),
                        record.Titre,
                        record.Description,
                        10, // LowStockThreshold
                        null, // SupplierId
                        record.CodeProjet,
                        record.Collection,
                        record.VolNo,
                        record.Type,
                        record.Annee,
                        record.PoidsCopieLb,
                        record.Compagnies);

                    await _products.AddAsync(product, cancellationToken);
                    created++;
                }
                else
                {
                    // Le produit existe déjà (même SKU) : on met à jour ses champs de catalogue
                    // avec les valeurs les plus récentes du fichier. Auparavant, cette branche se
                    // contentait d'incrémenter un compteur sans jamais appeler UpdateDetails — une
                    // réimportation d'un Excel corrigé n'avait donc aucun effet sur un produit déjà
                    // en base (Collection/Type/Vol. N°/Année/Compagnie restaient figés à leur valeur
                    // de création initiale). On ne remplace un champ par une valeur vide que si la
                    // ligne ne fournit vraiment rien, pour ne pas effacer des données valides avec
                    // un fichier partiellement rempli.
                    product.UpdateDetails(
                        Coalesce(record.Titre, product.Name)!,
                        Coalesce(record.Description, product.Description),
                        product.LowStockThreshold,
                        Coalesce(record.CodeProjet, product.ProjectCode),
                        Coalesce(record.Collection, product.Collection),
                        Coalesce(record.VolNo, product.VolumeNumber),
                        Coalesce(record.Type, product.ProductType),
                        record.Annee ?? product.Year,
                        record.PoidsCopieLb ?? product.WeightPerCopyLb,
                        Coalesce(record.Compagnies, product.Company));

                    updated++;
                }

                productsInBatch[record.CodeProduit] = product;

                if (!string.IsNullOrWhiteSpace(record.Location))
                {
                    var warehouse = await GetOrCreateWarehouseAsync(record.Location);
                    var stockKey = (product.Id, warehouse.Id);

                    // isNewProduct ne renseigne que sur le PRODUIT (SKU jamais vu avant dans le
                    // catalogue) — pas sur CE couple (produit, entrepôt) précis. Un même produit
                    // apparaît souvent à plusieurs emplacements (Boutique, Entrepôt, CAF...) dans
                    // le même import : se baser sur isNewProduct pour décider d'appliquer la
                    // quantité initiale laissait tous les emplacements SAUF le premier rencontré
                    // à 0 copie, même quand la ligne Excel en indiquait explicitement.
                    bool isNewStockRow;
                    Stock stock;
                    if (stocksInBatch.TryGetValue(stockKey, out var cachedStock))
                    {
                        stock = cachedStock;
                        isNewStockRow = false; // déjà traité plus tôt dans ce même import
                    }
                    else
                    {
                        var existingStock = await _stocks.GetAsync(product.Id, warehouse.Id, cancellationToken);
                        isNewStockRow = existingStock is null;
                        stock = existingStock ?? await _stocks.GetOrCreateAsync(product.Id, warehouse.Id, cancellationToken);
                    }

                    // Mise en cache IMMÉDIATE, avant tout appel qui pourrait lever une
                    // DomainException (Increase notamment) : si une ligne plus loin échoue et
                    // que le catch ci-dessous absorbe l'exception pour passer à la ligne
                    // suivante, ce stock déjà suivi par EF Core (GetOrCreateAsync l'a déjà
                    // ajouté) doit rester retrouvable pour ne pas en recréer un second en
                    // doublon à la prochaine occurrence de ce même (Produit, Entrepôt) — cause
                    // réelle observée d'une violation de contrainte unique au SaveChanges final.
                    stocksInBatch[stockKey] = stock;

                    stock.UpdateLogistics(
                        record.Section, record.Espace, record.PaletteCAF,
                        record.NbreDeBoites ?? 0, record.NbreCopiesBoite ?? 0,
                        ParseDate(record.DateEntree), ParseDate(record.DateSortie),
                        record.NomDistributeur, ParseDate(record.DateRetour), record.Commentaire);

                    // Prise d'inventaire physique (Date de prise inventaire / Nom du responsable),
                    // présente sur la même ligne que le reste dans le classeur réel.
                    stock.RecordInventoryTake(ParseDate(record.DatePriseInventaire), record.NomResponsable);

                    // record.NbreCopies peut légitimement valoir 0 (emplacement vérifié, aucune
                    // copie trouvée) : Stock.Increase() exige un montant strictement positif
                    // (c'est un mouvement d'entrée), donc 0 copie initiale ne doit tout simplement
                    // rien incrémenter — Stock.Create() initialise déjà la quantité à 0.
                    if (isNewStockRow && (record.NbreCopies ?? 0) > 0)
                    {
                        stock.Increase(record.NbreCopies!.Value);
                    }

                    if (stock.Id == Guid.Empty) // Newly created by GetOrCreateAsync
                    {
                        await _stocks.AddAsync(stock, cancellationToken);
                    }
                    else
                    {
                        _stocks.Update(stock);
                    }
                }
            }
            catch (DomainException ex)
            {
                errors.Add($"Le produit {record.CodeProduit} a été ignoré: {ex.Message}");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ImportResult(created, updated, errors);
    }
}
