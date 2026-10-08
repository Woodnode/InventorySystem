using InventorySystem.Application.Common;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Dashboard.Dtos;
using MediatR;

namespace InventorySystem.Application.Dashboard.Queries;

/// <summary>
/// Agregats du tableau de bord sur une fenetre glissante.
/// </summary>
/// <param name="PeriodDays">
/// Longueur de la fenetre. La periode precedente, de meme longueur, sert de point
/// de comparaison pour la variation.
/// </param>
public sealed record GetDashboardStatsQuery(int PeriodDays = 30)
    : IRequest<DashboardStatsDto>;

public sealed class GetDashboardStatsQueryHandler
    : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    /// <summary>Nombre de collections remontees au client.</summary>
    private const int TopCollections = 6;


    private readonly IProductRepository _products;
    private readonly IWarehouseRepository _warehouses;
    private readonly IStockMovementRepository _movements;
    private readonly IStockRepository _stocks;

    public GetDashboardStatsQueryHandler(
        IProductRepository products,
        IWarehouseRepository warehouses,
        IStockMovementRepository movements,
        IStockRepository stocks)
    {
        _products = products;
        _warehouses = warehouses;
        _movements = movements;
        _stocks = stocks;
    }

    public async Task<DashboardStatsDto> Handle(
        GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        // Borne haute : une fenetre trop large ferait parcourir tout l'historique.
        var periodDays = Math.Clamp(request.PeriodDays, 7, 180);

        // Journees pleines : on part du debut de la journee courante pour que la
        // derniere barre ne soit pas systematiquement tronquee par l'heure d'appel.
        var aujourdhui = DateTime.UtcNow.Date;
        var debutPeriode = aujourdhui.AddDays(-(periodDays - 1));
        var debutPrecedente = debutPeriode.AddDays(-periodDays);

        var totaux = await _products.GetCatalogTotalsAsync(cancellationToken);
        var collections = await _products.CountByCollectionAsync(TopCollections, cancellationToken);
        var entrepots = await _warehouses.ListAsync(QueryLimits.ReferenceMaxRows, cancellationToken);
        var charges = await _stocks.GetTotalsByWarehouseAsync(cancellationToken);

        // Un seul aller-retour couvre les deux periodes : on decoupe ensuite en memoire.
        var totauxParJour = await _movements.SumByDayAsync(debutPrecedente, cancellationToken);
        var parJour = totauxParJour.ToDictionary(t => DateOnly.FromDateTime(t.Day));

        // Les jours sans mouvement doivent apparaitre a zero, sinon la courbe se
        // contracte et donne a lire un rythme qui n'existe pas.
        var flux = new List<DailyFlowDto>(periodDays);
        for (var i = 0; i < periodDays; i++)
        {
            var jour = DateOnly.FromDateTime(debutPeriode.AddDays(i));
            if (parJour.TryGetValue(jour, out var t))
                flux.Add(new DailyFlowDto(jour, t.InQuantity, t.OutQuantity, t.InQuantity - t.OutQuantity));
            else
                flux.Add(new DailyFlowDto(jour, 0, 0, 0));
        }

        var netCourant = flux.Sum(f => f.Net);

        var debutPrecedenteOnly = DateOnly.FromDateTime(debutPrecedente);
        var debutPeriodeOnly = DateOnly.FromDateTime(debutPeriode);
        var netPrecedent = totauxParJour
            .Where(t =>
            {
                var j = DateOnly.FromDateTime(t.Day);
                return j >= debutPrecedenteOnly && j < debutPeriodeOnly;
            })
            .Sum(t => t.InQuantity - t.OutQuantity);

        // Une variation relative n'a pas de sens quand la reference est nulle :
        // « +100 % » a partir de zero serait une information inventee.
        double? variation = netPrecedent == 0
            ? null
            : Math.Round((netCourant - netPrecedent) / (double)Math.Abs(netPrecedent) * 100, 1);

        return new DashboardStatsDto(
            totaux.TotalProducts,
            Math.Round(totaux.TotalWeightGrams, 2),
            entrepots.Count(w => w.IsActive),
            entrepots.Count(w => !w.IsActive),
            collections.Select(c => new CollectionCountDto(c.Collection, c.Count)).ToList(),
            charges.Select(c => new WarehouseLoadDto(c.WarehouseId, c.WarehouseName, c.ProductCount, c.TotalQuantity)).ToList(),
            flux,
            netCourant,
            netPrecedent,
            variation,
            periodDays);
    }
}
