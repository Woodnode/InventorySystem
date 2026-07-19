namespace InventorySystem.Application.Common.Dtos;

/// <summary>Normalise page/pageSize reçus du client — évite qu'un pageSize absurde (0, négatif,
/// ou énorme) ne déclenche une requête coûteuse ou une division par zéro dans le calcul du Skip.</summary>
public static class Pagination
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));
}
