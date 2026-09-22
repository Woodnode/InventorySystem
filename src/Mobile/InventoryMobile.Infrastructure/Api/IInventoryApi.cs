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

    /// <summary>Paginé côté backend (le catalogue grandit sans borne) — voir <see cref="PagedResponse{T}"/>.</summary>
    [Get("/products")]
    Task<PagedResponse<ProductResponse>> GetProductsAsync(
        int page = 1, int pageSize = 100, CancellationToken ct = default);

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

    [Post("/movements")]
    Task<RecordMovementResult> RecordMovementAsync([Body] RecordMovementRequest request, CancellationToken ct = default);
}

// ── Pagination ───────────────────────────────────────────────────────────────

/// <summary>
/// Reflète PagedResult&lt;T&gt; côté backend. Le jalon 1 mobile n'a pas encore d'écran de
/// pagination (défilement infini / bouton "page suivante") : les appelants demandent une
/// grande page (voir GetProductsAsync/GetMovementsByProductAsync) plutôt que de parcourir
/// les pages. Un vrai contrôle de pagination fait partie du polish du jalon 2.
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

/// <summary>Reflète ProductDto côté backend (stock total agrégé sur tous les entrepôts).</summary>
public sealed record ProductResponse(
    Guid Id, string Sku, string Name, string? Description,
    int Quantity, int LowStockThreshold, bool IsLowOnStock);

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
