using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Infrastructure.Persistence;

/// <summary>
/// Jeu de données de démonstration d'une maison d'édition québécoise : fournisseurs, entrepôts,
/// catalogue réparti en collections, stocks par entrepôt et deux mois de mouvements.
///
/// Le premier jeu (douze produits, deux entrepôts, aucun mouvement) laissait le tableau de bord à
/// moitié vide : pas de flux de stock, pas de variation, une seule collection. Ici chaque bloc du
/// tableau de bord a de quoi s'afficher, dont quelques articles sous leur seuil d'alerte.
///
/// Les mouvements sont tirés d'un générateur à graine fixe : la démonstration est identique à
/// chaque régénération. N'agit que si la base est vide : une visite ne peut pas écraser des
/// données existantes. Activé par la configuration « Seed:Demo » (site vitrine), inactif ailleurs.
/// </summary>
public static class DemoDataSeeder
{
    private const int MovementDays = 60;
    private const string Company = "Éditions du Fleuve";

    public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await context.Products.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Données de démonstration déjà présentes, rien à faire.");
            return;
        }

        var suppliers = new[]
        {
            Supplier.Create("Papeterie Saint-Laurent",     "commandes@papeterie-sl.ca",         "418 555 0142"),
            Supplier.Create("Distribution Lavoie",         "ventes@lavoie-distribution.ca",     "514 555 0188"),
            Supplier.Create("Imprimerie Beauport",         "info@imprimerie-beauport.ca",       "418 555 0110"),
            Supplier.Create("Reliure Charlevoix",          "atelier@reliure-charlevoix.ca",     "418 555 0176"),
            Supplier.Create("Cartons Rive-Sud",            "service@cartons-rivesud.ca",        "450 555 0123"),
            Supplier.Create("Encres et toners Laurentides","commandes@encres-laurentides.ca",   "819 555 0164"),
            Supplier.Create("Diffusion Nordique",          "diffusion@nordique.ca",             "514 555 0137"),
            Supplier.Create("Atelier graphique Limoilou",  "bonjour@atelier-limoilou.ca",       "418 555 0191"),
        };
        context.Suppliers.AddRange(suppliers);

        var warehouses = new[]
        {
            Warehouse.Create("Entrepôt principal",     "2500 boulevard Wilfrid-Hamel, Québec"),
            Warehouse.Create("Entrepôt Montréal",      "1840 rue Notre-Dame Ouest, Montréal"),
            Warehouse.Create("Entrepôt Sherbrooke",    "455 rue King Ouest, Sherbrooke"),
            Warehouse.Create("Entrepôt Trois-Rivières","1200 boulevard des Forges, Trois-Rivières"),
            Warehouse.Create("Entrepôt Lévis",         "75 route du Président-Kennedy, Lévis"),
        };
        // Fermé pour travaux : l'indicateur des entrepôts inactifs du tableau de bord a un contenu.
        warehouses[4].Deactivate();
        context.Warehouses.AddRange(warehouses);
        await context.SaveChangesAsync(cancellationToken);

        // (SKU, nom, description, seuil, fournisseur, collection, type, année, grammes par exemplaire,
        //  quantités par entrepôt actif : Québec, Montréal, Sherbrooke, Trois-Rivières)
        var catalogue = new (string Sku, string Name, string Description, int Threshold, int Supplier,
            string Collection, string Type, int Year, decimal Grams, int[] Qty)[]
        {
            ("LIV-0001", "Atlas du Québec, édition 2026",        "Cartographie complète, couverture rigide", 15, 6, "Atlas et cartes", "Livre",      2026, 1450m, [120, 40, 22, 18]),
            ("LIV-0002", "Atlas routier du Saint-Laurent",       "Format à spirale, 220 pages",              12, 6, "Atlas et cartes", "Livre",      2025,  820m, [ 64, 30, 12,  9]),
            ("LIV-0003", "Carte murale des régions",             "Plastifiée, 120 x 90 cm",                   8, 7, "Atlas et cartes", "Affiche",    2025,  380m, [ 36, 14,  6,  4]),
            ("LIV-0004", "Atlas des parcs nationaux",            "Illustré, 312 pages",                      10, 6, "Atlas et cartes", "Livre",      2024, 1120m, [ 18,  6,  3,  2]),
            ("LIV-0101", "Guide des oiseaux du Saint-Laurent",   "Illustré, 480 pages",                      10, 6, "Nature",          "Livre",      2024,  960m, [ 64, 18, 15, 11]),
            ("LIV-0102", "Arbres et arbustes du Québec",         "Guide d'identification, 360 pages",        10, 6, "Nature",          "Livre",      2023,  780m, [ 42, 21,  9,  7]),
            ("LIV-0103", "Champignons comestibles",              "Format de poche, 210 pages",               12, 6, "Nature",          "Livre",      2025,  340m, [ 27, 10,  4,  3]),
            ("LIV-0104", "Fleurs sauvages des Laurentides",      "Couverture souple, 256 pages",             10, 6, "Nature",          "Livre",      2026,  510m, [  5,  2,  1,  0]),
            ("LIV-0105", "Carnet d'observation des étoiles",     "Cartes du ciel saisonnières",               8, 7, "Nature",          "Livre",      2025,  290m, [ 33, 12,  8,  6]),
            ("LIV-0201", "Recueil de nouvelles, tome I",         "Format poche",                             20, 6, "Romans",          "Livre",      2024,  240m, [ 88, 45, 20, 16]),
            ("LIV-0202", "Recueil de nouvelles, tome II",        "Format poche",                             20, 6, "Romans",          "Livre",      2025,  240m, [  8,  3,  2,  1]),
            ("LIV-0203", "Le phare de Kamouraska",               "Roman, 384 pages",                         15, 6, "Romans",          "Livre",      2026,  420m, [140, 72, 31, 25]),
            ("LIV-0204", "Hiver sur la Côte-Nord",               "Roman, 296 pages",                         15, 6, "Romans",          "Livre",      2025,  360m, [ 56, 28, 14, 10]),
            ("LIV-0205", "Les saisons de l'île d'Orléans",       "Roman, 412 pages",                         15, 6, "Romans",          "Livre",      2024,  450m, [ 23, 11,  5,  4]),
            ("LIV-0206", "Chroniques de la rue Saint-Jean",      "Récits, 228 pages",                        12, 6, "Romans",          "Livre",      2023,  300m, [ 49, 17,  9,  6]),
            ("LIV-0301", "Mon premier imagier",                  "Cartonné, 24 pages",                       25, 7, "Jeunesse",        "Livre",      2026,  310m, [210, 95, 48, 37]),
            ("LIV-0302", "Le castor qui ne dormait pas",         "Album illustré, 32 pages",                 20, 7, "Jeunesse",        "Livre",      2025,  280m, [ 96, 52, 26, 19]),
            ("LIV-0303", "Cahier d'activités des vacances",      "Jeux et coloriages, 64 pages",             30, 7, "Jeunesse",        "Cahier",     2026,  190m, [ 12,  6,  3,  2]),
            ("LIV-0304", "Contes du Grand Nord",                 "Recueil illustré, 96 pages",               15, 7, "Jeunesse",        "Livre",      2024,  420m, [ 61, 30, 13, 11]),
            ("LIV-0305", "J'apprends à compter",                 "Cartonné, 20 pages",                       25, 7, "Jeunesse",        "Livre",      2025,  260m, [134, 70, 29, 22]),
            ("PAP-0100", "Rames de papier A4, 80 g",             "Boîte de 5 rames",                         25, 0, "Fournitures",     "Papeterie",  2026, 12500m,[180, 90, 40, 35]),
            ("PAP-0101", "Cahiers spiralés 200 pages",           "Paquet de 10",                             30, 0, "Fournitures",     "Papeterie",  2026, 2100m, [ 14,  6,  4,  2]),
            ("PAP-0102", "Enveloppes format lettre",             "Boîte de 500",                             20, 0, "Fournitures",     "Papeterie",  2025, 3400m, [ 95, 25, 18, 12]),
            ("PAP-0103", "Carnets de notes lignés",              "Paquet de 12",                             24, 0, "Fournitures",     "Papeterie",  2026, 1800m, [ 76, 33, 15, 12]),
            ("BUR-0200", "Classeurs à anneaux 2 pouces",         "Paquet de 6",                              12, 1, "Fournitures",     "Bureau",     2025, 2600m, [ 48, 16, 10,  8]),
            ("BUR-0201", "Boîtes d'archivage",                   "Paquet de 12",                             15, 4, "Fournitures",     "Bureau",     2025, 4200m, [  6,  3,  2,  1]),
            ("BUR-0202", "Stylos à bille bleus",                 "Boîte de 50",                              40, 1, "Fournitures",     "Bureau",     2026,  650m, [240,120, 60, 45]),
            ("BUR-0203", "Agrafeuses de bureau",                 "Capacité 25 feuilles",                     10, 1, "Fournitures",     "Bureau",     2024,  380m, [ 31, 14,  7,  5]),
            ("EMB-0400", "Boîtes d'expédition moyennes",         "Paquet de 25, carton double cannelure",    20, 4, "Expédition",      "Emballage",  2026, 5400m, [ 85, 40, 22, 18]),
            ("EMB-0401", "Rouleaux de ruban adhésif",            "Paquet de 6",                              15, 4, "Expédition",      "Emballage",  2026,  960m, [ 44, 20, 11,  9]),
            ("EMB-0402", "Enveloppes matelassées",               "Boîte de 100",                             12, 4, "Expédition",      "Emballage",  2025, 2900m, [  5,  3,  1,  1]),
            ("EMB-0403", "Papier bulle en rouleau",              "50 mètres",                                 6, 4, "Expédition",      "Emballage",  2025, 3800m, [ 19,  8,  4,  3]),
            ("IMP-0300", "Cartouches d'encre noire",             "Compatibles série 400",                    10, 5, "Impression",      "Consommable",2026,  120m, [ 31, 12,  6,  5]),
            ("IMP-0301", "Toner couleur",                        "Rendement 2 500 pages",                     8, 5, "Impression",      "Consommable",2026,  860m, [  5,  2,  1,  0]),
            ("IMP-0302", "Rouleaux d'étiquettes",                "Paquet de 4",                              18, 2, "Impression",      "Consommable",2025,  540m, [ 60, 20, 12,  9]),
            ("IMP-0303", "Papier photo glacé",                   "Paquet de 100 feuilles",                   10, 2, "Impression",      "Consommable",2025, 1650m, [ 26, 10,  5,  3]),
        };

        var activeWarehouses = warehouses[..4];
        var sections = new[] { "A", "B", "C", "D" };
        var products = new List<Product>(catalogue.Length);
        var random = new Random(2026);
        var today = DateTime.UtcNow.Date;

        for (var index = 0; index < catalogue.Length; index++)
        {
            var item = catalogue[index];
            var product = Product.Create(
                Sku.Create(item.Sku), item.Name, item.Description, item.Threshold, suppliers[item.Supplier].Id,
                projectCode: $"PRJ-{item.Year}-{index + 1:000}",
                collection: item.Collection,
                productType: item.Type,
                year: item.Year,
                weightPerCopyGrams: item.Grams,
                company: Company);
            context.Products.Add(product);
            await context.SaveChangesAsync(cancellationToken); // l'identifiant du produit est requis pour le stock
            products.Add(product);

            for (var w = 0; w < activeWarehouses.Length; w++)
            {
                var quantity = item.Qty[w];
                if (quantity == 0)
                    continue;

                var stock = Stock.Create(product.Id, activeWarehouses[w].Id, quantity);
                var copiesPerBox = item.Type == "Livre" ? 12 : 6;
                stock.UpdateLogistics(
                    section: sections[index % sections.Length],
                    space: $"{(index % 12) + 1:00}",
                    pallet: $"P-{w + 1}{index + 10:00}",
                    boxesCount: Math.Max(1, quantity / copiesPerBox),
                    copiesPerBox: copiesPerBox,
                    entryDate: today.AddDays(-random.Next(20, 160)),
                    exitDate: null,
                    distributorName: suppliers[item.Supplier].Name,
                    returnDate: null,
                    comment: null);
                context.Stocks.Add(stock);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var movementCount = SeedMovements(context, products, activeWarehouses, random, today);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Données de démonstration créées : {Suppliers} fournisseurs, {Warehouses} entrepôts, {Products} produits, {Movements} mouvements.",
            suppliers.Length, warehouses.Length, products.Count, movementCount);
    }

    /// <summary>
    /// Deux mois de réceptions, d'expéditions et de transferts. Le stock se reconstitue sur les deux
    /// périodes, plus vite ce mois-ci (réimpressions de la rentrée) : la variation affichée est une
    /// hausse lisible, pas le saut d'un solde négatif à un solde positif.
    /// </summary>
    private static int SeedMovements(AppDbContext context, List<Product> products, Warehouse[] warehouses, Random random, DateTime today)
    {
        string[] inReasons = ["Réception fournisseur", "Retour de librairie", "Réimpression", "Réception commande ouverte"];
        string[] outReasons = ["Commande librairie", "Expédition en ligne", "Salon du livre", "Commande scolaire", "Envoi de presse"];
        string[] transferReasons = ["Réassort régional", "Équilibrage des stocks", "Préparation d'un salon"];

        var count = 0;
        for (var day = MovementDays; day >= 0; day--)
        {
            var date = today.AddDays(-day);
            // Peu d'activité le week-end, davantage en semaine.
            var dailyMovements = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                ? random.Next(0, 3)
                : random.Next(3, 7);
            var currentMonth = day <= 30;

            for (var m = 0; m < dailyMovements; m++)
            {
                var product = products[random.Next(products.Count)];
                var source = warehouses[random.Next(warehouses.Length)];
                var roll = random.NextDouble();
                var outShare = currentMonth ? 0.36 : 0.46;

                StockMovement movement;
                if (roll < 0.12)
                {
                    var destination = warehouses.First(w => w.Id != source.Id);
                    movement = StockMovement.Record(product.Id, source.Id, MovementType.Transfer, random.Next(4, 30),
                        transferReasons[random.Next(transferReasons.Length)], destination.Id);
                }
                else if (roll < 0.12 + outShare)
                {
                    movement = StockMovement.Record(product.Id, source.Id, MovementType.Out, random.Next(3, 40),
                        outReasons[random.Next(outReasons.Length)]);
                }
                else
                {
                    movement = StockMovement.Record(product.Id, source.Id, MovementType.In, random.Next(12, 70),
                        inReasons[random.Next(inReasons.Length)]);
                }

                context.StockMovements.Add(movement);
                // La date d'un mouvement est sa date de création : on la recule pour étaler l'historique.
                var at = date.AddHours(random.Next(8, 18)).AddMinutes(random.Next(0, 60));
                context.Entry(movement).Property(nameof(StockMovement.CreatedAtUtc)).CurrentValue = at;
                context.Entry(movement).Property(nameof(StockMovement.UpdatedAtUtc)).CurrentValue = at;
                count++;
            }
        }

        return count;
    }
}
