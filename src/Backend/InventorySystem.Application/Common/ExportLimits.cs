namespace InventorySystem.Application.Common;

/// <summary>
/// Plafonds pour éviter OOM / timeouts lorsque les volumes grossissent.
/// </summary>
public static class QueryLimits
{
    public const int ExportMaxRows = 10_000;
    /// <summary>Plafond listes référentielles (entrepôts / fournisseurs).</summary>
    public const int ReferenceMaxRows = 1_000;
}

/// <summary>Alias historique pour les exports.</summary>
public static class ExportLimits
{
    public const int MaxRows = QueryLimits.ExportMaxRows;
}
