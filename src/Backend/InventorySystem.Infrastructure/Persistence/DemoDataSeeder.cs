using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Infrastructure.Persistence;

/// <summary>
/// Jeu de données de démonstration d'une librairie fictive : fournisseurs, entrepôts, catalogue de
/// livres connus réparti en collections, stocks par entrepôt et deux mois de mouvements.
///
/// Tout est inventé ou public : titres de livres célèbres, entrepôts et adresses génériques.
/// Aucune donnée d'une entreprise réelle ne doit apparaître sur la vitrine.
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
    private const string Company = "Librairie du Fleuve";

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
            Warehouse.Create("Entrepôt central",            "1200 rue de l'Industrie"),
            Warehouse.Create("Centre de distribution Nord", "45 avenue des Entrepreneurs"),
            Warehouse.Create("Entrepôt Est",                "780 boulevard du Commerce"),
            Warehouse.Create("Entrepôt Sud",                "15 rue des Artisans"),
            Warehouse.Create("Entrepôt Ouest",              "330 chemin du Port"),
        };
        // Fermé pour travaux : l'indicateur des entrepôts inactifs du tableau de bord a un contenu.
        warehouses[4].Deactivate();
        context.Warehouses.AddRange(warehouses);
        await context.SaveChangesAsync(cancellationToken);

        // (SKU, nom, description, seuil, fournisseur, collection, type, année, grammes par exemplaire,
        //  quantités par entrepôt actif : central, Nord, Est, Sud)
        var catalogue = new (string Sku, string Name, string Description, int Threshold, int Supplier,
            string Collection, string Type, int Year, decimal Grams, int[] Qty)[]
        {
            ("LIV-0001", "Le Seigneur des anneaux : La Communauté de l'anneau", "J.R.R. Tolkien, poche",          15, 6, "Fantasy",          "Livre",          2023,  380m, [ 96, 40, 22, 18]),
            ("LIV-0002", "Le Seigneur des anneaux : Les Deux Tours",            "J.R.R. Tolkien, poche",          15, 6, "Fantasy",          "Livre",          2023,  360m, [ 72, 30, 15, 12]),
            ("LIV-0003", "Le Seigneur des anneaux : Le Retour du roi",          "J.R.R. Tolkien, poche",          15, 6, "Fantasy",          "Livre",          2023,  400m, [  6,  3,  2,  1]),
            ("LIV-0004", "Le Hobbit",                                           "J.R.R. Tolkien, illustré",       12, 6, "Fantasy",          "Livre",          2024,  520m, [ 64, 28, 12,  9]),
            ("LIV-0005", "Le Silmarillion",                                     "J.R.R. Tolkien",                  8, 6, "Fantasy",          "Livre",          2022,  560m, [ 18,  6,  3,  2]),
            ("LIV-0006", "Le Trône de fer, tome 1",                             "George R.R. Martin",             10, 1, "Fantasy",          "Livre",          2024,  610m, [ 42, 20,  9,  7]),
            ("LIV-0007", "Le Nom du vent",                                      "Patrick Rothfuss",               10, 1, "Fantasy",          "Livre",          2023,  720m, [  4,  2,  1,  1]),
            ("LIV-0101", "Harry Potter à l'école des sorciers",                 "J.K. Rowling, grand format",     20, 6, "Jeunesse",         "Livre",          2023,  450m, [140, 72, 31, 25]),
            ("LIV-0102", "Harry Potter et la Chambre des secrets",              "J.K. Rowling, grand format",     20, 6, "Jeunesse",         "Livre",          2023,  480m, [110, 55, 24, 19]),
            ("LIV-0103", "Harry Potter et le Prisonnier d'Azkaban",             "J.K. Rowling, grand format",     20, 6, "Jeunesse",         "Livre",          2023,  520m, [ 88, 45, 20, 16]),
            ("LIV-0104", "Harry Potter et la Coupe de feu",                     "J.K. Rowling, grand format",     20, 6, "Jeunesse",         "Livre",          2023,  780m, [ 10,  4,  3,  1]),
            ("LIV-0105", "Harry Potter et l'Ordre du phénix",                   "J.K. Rowling, grand format",     15, 6, "Jeunesse",         "Livre",          2023,  880m, [ 56, 28, 14, 10]),
            ("LIV-0106", "Harry Potter et le Prince de sang-mêlé",              "J.K. Rowling, grand format",     15, 6, "Jeunesse",         "Livre",          2023,  720m, [ 49, 17,  9,  6]),
            ("LIV-0107", "Harry Potter et les Reliques de la Mort",             "J.K. Rowling, grand format",     15, 6, "Jeunesse",         "Livre",          2023,  790m, [ 61, 30, 13, 11]),
            ("LIV-0108", "Le Petit Prince",                                     "Antoine de Saint-Exupéry",       25, 7, "Jeunesse",         "Livre",          2021,  210m, [210, 95, 48, 37]),
            ("LIV-0109", "Charlie et la chocolaterie",                          "Roald Dahl",                     15, 7, "Jeunesse",         "Livre",          2022,  230m, [ 33, 12,  8,  6]),
            ("LIV-0110", "Le Lion, la Sorcière blanche et l'Armoire magique",   "C.S. Lewis, Le Monde de Narnia", 12, 7, "Jeunesse",         "Livre",          2022,  300m, [ 27, 10,  4,  3]),
            ("LIV-0111", "Hunger Games",                                        "Suzanne Collins",                12, 1, "Jeunesse",         "Livre",          2024,  340m, [  5,  3,  1,  1]),
            ("LIV-0201", "Les Misérables",                                      "Victor Hugo, édition intégrale", 10, 2, "Classiques",       "Livre",          2020, 1200m, [ 23, 11,  5,  4]),
            ("LIV-0202", "Le Comte de Monte-Cristo",                            "Alexandre Dumas, deux tomes",     8, 2, "Classiques",       "Coffret",        2021, 1350m, [ 19,  8,  4,  3]),
            ("LIV-0203", "Orgueil et Préjugés",                                 "Jane Austen",                    12, 2, "Classiques",       "Livre",          2022,  320m, [ 48, 16, 10,  8]),
            ("LIV-0204", "Vingt mille lieues sous les mers",                    "Jules Verne, illustré",          10, 3, "Classiques",       "Livre",          2023,  640m, [ 31, 14,  7,  5]),
            ("LIV-0205", "L'Étranger",                                          "Albert Camus, poche",            15, 2, "Classiques",       "Livre",          2022,  140m, [ 76, 33, 15, 12]),
            ("LIV-0206", "Don Quichotte",                                       "Miguel de Cervantès",             6, 3, "Classiques",       "Livre",          2021,  980m, [  4,  1,  0,  0]),
            ("LIV-0301", "Dune",                                                "Frank Herbert",                  12, 1, "Science-fiction",  "Livre",          2024,  690m, [ 85, 40, 22, 18]),
            ("LIV-0302", "Fondation",                                           "Isaac Asimov",                   10, 1, "Science-fiction",  "Livre",          2023,  330m, [ 44, 20, 11,  9]),
            ("LIV-0303", "1984",                                                "George Orwell, poche",           20, 2, "Science-fiction",  "Livre",          2022,  220m, [ 95, 25, 18, 12]),
            ("LIV-0304", "Le Guide du voyageur galactique",                     "Douglas Adams",                  10, 1, "Science-fiction",  "Livre",          2023,  260m, [ 26, 10,  5,  3]),
            ("LIV-0305", "Fahrenheit 451",                                      "Ray Bradbury",                   12, 2, "Science-fiction",  "Livre",          2022,  180m, [ 60, 20, 12,  9]),
            ("LIV-0401", "Le Crime de l'Orient-Express",                        "Agatha Christie",                12, 1, "Policier",         "Livre",          2023,  250m, [ 36, 14,  6,  4]),
            ("LIV-0402", "Ils étaient dix",                                     "Agatha Christie",                12, 1, "Policier",         "Livre",          2023,  240m, [ 48, 16, 10,  8]),
            ("LIV-0403", "Le Chien des Baskerville",                            "Arthur Conan Doyle",             10, 3, "Policier",         "Livre",          2022,  230m, [ 19,  8,  4,  3]),
            ("LIV-0404", "Millénium, tome 1",                                   "Stieg Larsson",                  10, 1, "Policier",         "Livre",          2023,  760m, [  5,  2,  1,  1]),
            ("LIV-0501", "Astérix le Gaulois",                                  "Goscinny et Uderzo",             15, 7, "Bande dessinée",   "Album",          2023,  410m, [240,120, 60, 45]),
            ("LIV-0502", "Tintin au Tibet",                                     "Hergé",                          12, 7, "Bande dessinée",   "Album",          2022,  420m, [ 31, 12,  6,  5]),
            ("LIV-0503", "Les Schtroumpfs noirs",                               "Peyo",                           10, 7, "Bande dessinée",   "Album",          2022,  380m, [ 26, 10,  5,  3]),
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
    /// périodes, plus vite ce mois-ci (réassort de la rentrée) : la variation affichée est une
    /// hausse lisible, pas le saut d'un solde négatif à un solde positif.
    /// </summary>
    private static int SeedMovements(AppDbContext context, List<Product> products, Warehouse[] warehouses, Random random, DateTime today)
    {
        string[] inReasons = ["Réception fournisseur", "Retour client", "Réassort éditeur", "Réception commande ouverte"];
        string[] outReasons = ["Vente en magasin", "Commande en ligne", "Salon du livre", "Commande scolaire", "Commande de bibliothèque"];
        string[] transferReasons = ["Réassort régional", "Équilibrage des stocks", "Préparation d'un salon"];

        var count = 0;
        var maintenant = DateTime.UtcNow;
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
                // Le jour même, une heure tirée au hasard pouvait tomber après l'instant présent :
                // le tableau de bord affichait alors un mouvement « dans 17 minutes ».
                if (at > maintenant)
                    at = maintenant.AddMinutes(-random.Next(5, 90));
                context.Entry(movement).Property(nameof(StockMovement.CreatedAtUtc)).CurrentValue = at;
                context.Entry(movement).Property(nameof(StockMovement.UpdatedAtUtc)).CurrentValue = at;
                count++;
            }
        }

        return count;
    }
}
