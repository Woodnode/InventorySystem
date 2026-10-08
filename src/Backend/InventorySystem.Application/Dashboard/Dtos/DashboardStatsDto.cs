namespace InventorySystem.Application.Dashboard.Dtos;

/// <summary>Flux d'une journée : entrées, sorties, et solde net.</summary>
/// <remarks>
/// Les transferts sont volontairement exclus : ils déplacent du stock d'un entrepôt
/// à l'autre sans rien ajouter ni retirer au total détenu.
/// </remarks>
public sealed record DailyFlowDto(DateOnly Date, int In, int Out, int Net);

/// <summary>Charge d'un entrepôt : références détenues et unités stockées.</summary>
public sealed record WarehouseLoadDto(Guid WarehouseId, string Name, int ProductCount, int TotalQuantity);

/// <summary>Nombre de références portant une même collection.</summary>
public sealed record CollectionCountDto(string Collection, int Count);

/// <summary>
/// Agrégats du tableau de bord, calculés en base.
///
/// Sans cet endpoint, le client devait paginer le catalogue entier pour obtenir le
/// nombre de références et le poids cumulé — des dizaines d'appels à chaque visite.
/// </summary>
/// <param name="TotalProducts">Nombre de références au catalogue.</param>
/// <param name="TotalWeightGrams">Poids cumulé du stock détenu, en livres.</param>
/// <param name="TopCollections">Collections les plus fournies, décroissant.</param>
/// <param name="DailyFlow">Une entrée par jour de la période, jours sans mouvement compris.</param>
/// <param name="NetCurrentPeriod">Solde entrées moins sorties sur la période.</param>
/// <param name="NetPreviousPeriod">Même solde sur la période immédiatement précédente.</param>
/// <param name="ChangePercent">
/// Variation entre les deux périodes, en pourcentage. <c>null</c> quand la période
/// précédente est à zéro : aucune variation relative n'est calculable à partir de rien.
/// </param>
public sealed record DashboardStatsDto(
    int TotalProducts,
    decimal TotalWeightGrams,
    int ActiveWarehouses,
    int InactiveWarehouses,
    IReadOnlyList<CollectionCountDto> TopCollections,
    IReadOnlyList<WarehouseLoadDto> WarehouseLoads,
    IReadOnlyList<DailyFlowDto> DailyFlow,
    int NetCurrentPeriod,
    int NetPreviousPeriod,
    double? ChangePercent,
    int PeriodDays);
