using System.Text.Json.Serialization;
using Refit;

namespace InventoryMobile.Infrastructure.Api;

/// <summary>
/// Contrat Refit consommant EXACTEMENT les mêmes endpoints REST que le frontend React
/// (voir plan §9) — preuve que l'API est réellement découplée de tout client. Les routes
/// et formes de DTO sont recopiées des controllers/DTOs backend actuels (Auth/Products/
/// Warehouses/Movements), pas devinées.
/// </summary>
public interface IInventoryApi
{
    [Post("/auth/login")]
    Task<AuthResultResponse> LoginAsync([Body] LoginRequest request, CancellationToken ct = default);

    [Post("/auth/refresh")]
    Task<AuthResultResponse> RefreshAsync([Body] RefreshTokenRequest request, CancellationToken ct = default);

    /// <summary>
    /// Révoque côté serveur toute la famille du refresh token (endpoint anonyme, 204).
    /// Sans cet appel, un refresh token copié depuis l'appareil restait utilisable après la
    /// déconnexion jusqu'à son expiration.
    /// </summary>
    [Post("/auth/logout")]
    Task LogoutAsync([Body] RefreshTokenRequest request, CancellationToken ct = default);

    /// <summary>
    /// Paginé côté backend (le catalogue grandit sans borne) — voir <see cref="PagedResponse{T}"/>.
    /// <paramref name="search"/> porte sur le SKU et le nom, comme la recherche du web ; un
    /// paramètre nul n'est pas envoyé.
    /// </summary>
    [Get("/products")]
    Task<PagedResponse<ProductResponse>> GetProductsAsync(
        int page = 1, int pageSize = 100, string? search = null, bool? lowStockOnly = null,
        CancellationToken ct = default);

    /// <summary>Un produit par id, avec son stock total agrégé (fiche produit).</summary>
    [Get("/products/{id}")]
    Task<ProductResponse> GetProductAsync(Guid id, CancellationToken ct = default);

    /// <summary>Paginé côté backend — alertes stock bas.</summary>
    [Get("/products/low-stock")]
    Task<PagedResponse<ProductResponse>> GetLowStockAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>Recherche exacte par SKU — alimente le scan (le code scanné == le SKU).</summary>
    [Get("/products/by-sku/{sku}")]
    Task<ProductResponse> GetProductBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>
    /// Non paginé côté backend : liste de référence de petite taille, réutilisée telle
    /// quelle comme source du sélecteur d'entrepôt dans le formulaire de mouvement.
    /// </summary>
    [Get("/warehouses")]
    Task<IReadOnlyList<WarehouseResponse>> GetWarehousesAsync(CancellationToken ct = default);

    /// <summary>Paginé côté backend (l'historique grandit sans borne) — voir <see cref="PagedResponse{T}"/>.</summary>
    [Get("/movements/product/{productId}")]
    Task<PagedResponse<MovementResponse>> GetMovementsByProductAsync(
        Guid productId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>Derniers mouvements tous produits confondus (journal de l'écran Mouvements du web).</summary>
    [Get("/movements/recent")]
    Task<IReadOnlyList<RecentMovementResponse>> GetRecentMovementsAsync(int take = 20, CancellationToken ct = default);

    /// <summary>Répartition du stock d'un produit, entrepôt par entrepôt.</summary>
    [Get("/stocks/product/{productId}")]
    Task<IReadOnlyList<StockResponse>> GetStocksByProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Agrégats du tableau de bord, calculés en base sur une fenêtre glissante.</summary>
    [Get("/dashboard/stats")]
    Task<DashboardStatsResponse> GetDashboardStatsAsync(int periodDays = 30, CancellationToken ct = default);

    [Post("/movements")]
    Task<RecordMovementResult> RecordMovementAsync([Body] RecordMovementRequest request, CancellationToken ct = default);
}

// ── Pagination ───────────────────────────────────────────────────────────────

/// <summary>
/// Reflète PagedResult&lt;T&gt; côté backend. Le catalogue mobile charge vingt produits par page
/// au fil du défilement ; l'historique d'un produit, dix mouvements par page.
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

// ── Auth ─────────────────────────────────────────────────────────────────────

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

/// <summary>Reflète AuthResultDto côté backend (System.Text.Json sérialise en camelCase).</summary>
public sealed record AuthResultResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

// ── Produits ─────────────────────────────────────────────────────────────────

/// <summary>
/// Reflète ProductDto côté backend (stock total agrégé sur tous les entrepôts). Les champs
/// descriptifs sont optionnels : un produit créé sans eux les renvoie à null.
/// </summary>
public sealed record ProductResponse(
    Guid Id, string Sku, string Name, string? Description,
    int Quantity, int LowStockThreshold, bool IsLowOnStock,
    string? ProjectCode = null, string? Collection = null, string? VolumeNumber = null,
    string? ProductType = null, int? Year = null, decimal? WeightPerCopyGrams = null,
    string? Company = null, string? PrimaryLocation = null);

// ── Stocks ───────────────────────────────────────────────────────────────────

/// <summary>Reflète StockDto côté backend : quantité et emplacement dans un entrepôt.</summary>
public sealed record StockResponse(
    Guid ProductId, Guid WarehouseId, int Quantity,
    string? Section, string? Space, string? Pallet,
    int BoxesCount, int CopiesPerBox,
    DateTime? EntryDate, DateTime? ExitDate, string? DistributorName,
    DateTime? ReturnDate, string? Comment, DateTime? InventoryDate, string? ResponsibleName);

// ── Tableau de bord ──────────────────────────────────────────────────────────

/// <summary>Reflète DailyFlowDto : entrées, sorties et solde d'une journée.</summary>
public sealed record DailyFlowResponse(DateOnly Date, int In, int Out, int Net);

/// <summary>Reflète WarehouseLoadDto : références et unités détenues par un entrepôt.</summary>
public sealed record WarehouseLoadResponse(Guid WarehouseId, string Name, int ProductCount, int TotalQuantity);

/// <summary>Reflète CollectionCountDto.</summary>
public sealed record CollectionCountResponse(string Collection, int Count);

/// <summary>Reflète DashboardStatsDto côté backend.</summary>
public sealed record DashboardStatsResponse(
    int TotalProducts,
    decimal TotalWeightGrams,
    int ActiveWarehouses,
    int InactiveWarehouses,
    IReadOnlyList<CollectionCountResponse> TopCollections,
    IReadOnlyList<WarehouseLoadResponse> WarehouseLoads,
    IReadOnlyList<DailyFlowResponse> DailyFlow,
    int NetCurrentPeriod,
    int NetPreviousPeriod,
    double? ChangePercent,
    int PeriodDays);

// ── Entrepôts ────────────────────────────────────────────────────────────────

/// <summary>Reflète WarehouseDto côté backend.</summary>
public sealed record WarehouseResponse(Guid Id, string Name, string? Address, bool IsActive);

// ── Mouvements ───────────────────────────────────────────────────────────────

/// <summary>Miroir de MovementType côté backend — sérialisé en string (JsonStringEnumConverter global).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MobileMovementType
{
    In = 1,
    Out = 2,
    Transfer = 3,
}

/// <summary>Reflète StockMovementDto côté backend.</summary>
public sealed record MovementResponse(
    Guid Id, Guid ProductId, Guid WarehouseId, Guid? ToWarehouseId,
    MobileMovementType Type, int Quantity, string? Reason, DateTime CreatedAtUtc);

/// <summary>
/// Reflète RecordMovementCommand côté backend. <see cref="ClientGuid"/> est toujours fourni
/// côté mobile (contrairement au web, où il est optionnel) : il garantit l'idempotence de la
/// synchronisation offline — voir <see cref="Persistence.LocalMovement"/> et plan §8.3.
/// </summary>
public sealed record RecordMovementRequest(
    Guid ProductId,
    Guid WarehouseId,
    MobileMovementType Type,
    int Quantity,
    string? Reason,
    Guid? ToWarehouseId,
    Guid ClientGuid);

public sealed record RecordMovementResult(Guid Id);

/// <summary>Reflète RecentMovementDto côté backend : mouvement avec noms de produit et d'entrepôts.</summary>
public sealed record RecentMovementResponse(
    Guid Id, Guid ProductId, string ProductSku, string ProductName,
    string WarehouseName, string? ToWarehouseName,
    MobileMovementType Type, int Quantity, string? Reason, DateTime CreatedAtUtc);
