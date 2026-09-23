using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Infrastructure.Persistence;

/// <summary>
/// Jeu de données de démonstration : fournisseurs, entrepôts, produits et stocks, dont quelques
/// articles sous leur seuil d'alerte pour que le tableau de bord et les alertes aient du contenu.
///
/// N'agit que si la base est vide : une visite ne peut pas écraser des données existantes.
/// Activé par la configuration « Seed:Demo » (site vitrine), inactif ailleurs.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await context.Products.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Données de démonstration déjà présentes, rien à faire.");
            return;
        }

        var suppliers = new[]
        {
            Supplier.Create("Papeterie Saint-Laurent", "commandes@papeterie-sl.ca", "418 555 0142"),
            Supplier.Create("Distribution Lavoie",     "ventes@lavoie-distribution.ca", "514 555 0188"),
            Supplier.Create("Imprimerie Beauport",     "info@imprimerie-beauport.ca", "418 555 0110"),
        };
        context.Suppliers.AddRange(suppliers);

        var warehouses = new[]
        {
            Warehouse.Create("Entrepôt principal", "2500 boulevard Wilfrid-Hamel, Québec"),
            Warehouse.Create("Entrepôt Montréal",  "1840 rue Notre-Dame Ouest, Montréal"),
        };
        context.Warehouses.AddRange(warehouses);
        await context.SaveChangesAsync(cancellationToken);

        // (SKU, nom, seuil d'alerte, fournisseur, quantité principale, quantité Montréal)
        var catalogue = new (string Sku, string Name, string Description, int Threshold, int Supplier, int QtyMain, int QtyMtl)[]
        {
            ("LIV-0001", "Atlas du Québec, édition 2026", "Cartographie complète, couverture rigide", 15, 2, 120,  40),
            ("LIV-0002", "Guide des oiseaux du Saint-Laurent", "Illustré, 480 pages",                 10,  2,  64,  18),
            ("LIV-0003", "Recueil de nouvelles, tome II", "Format poche",                             20,  2,   8,   3),
            ("PAP-0100", "Rames de papier A4, 80 g",     "Boîte de 5 rames",                          25,  0, 180,  90),
            ("PAP-0101", "Cahiers spiralés 200 pages",   "Paquet de 10",                              30,  0,  22,  11),
            ("PAP-0102", "Enveloppes format lettre",     "Boîte de 500",                              20,  0,  95,  25),
            ("BUR-0200", "Classeurs à anneaux 2 pouces", "Paquet de 6",                               12,  1,  48,  16),
            ("BUR-0201", "Boîtes d'archivage",           "Paquet de 12",                              15,  1,   9,   4),
            ("BUR-0202", "Stylos à bille bleus",         "Boîte de 50",                               40,  1, 240, 120),
            ("IMP-0300", "Cartouches d'encre noire",     "Compatibles série 400",                     10,  2,  31,  12),
            ("IMP-0301", "Toner couleur",                "Rendement 2 500 pages",                      8,  2,   5,   2),
            ("IMP-0302", "Rouleaux d'étiquettes",        "Paquet de 4",                               18,  0,  60,  20),
        };

        foreach (var item in catalogue)
        {
            var product = Product.Create(
                Sku.Create(item.Sku), item.Name, item.Description, item.Threshold, suppliers[item.Supplier].Id);
            context.Products.Add(product);
            await context.SaveChangesAsync(cancellationToken); // l'identifiant du produit est requis pour le stock

            context.Stocks.Add(Stock.Create(product.Id, warehouses[0].Id, item.QtyMain));
            context.Stocks.Add(Stock.Create(product.Id, warehouses[1].Id, item.QtyMtl));
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Données de démonstration créées : {Suppliers} fournisseurs, {Warehouses} entrepôts, {Products} produits.",
            suppliers.Length, warehouses.Length, catalogue.Length);
    }
}
