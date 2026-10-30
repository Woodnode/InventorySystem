using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;

namespace InventorySystem.Backend.UnitTests.Application.Products;

/// <summary>
/// Dépôts en mémoire pour l'import : l'import enchaîne recherches et ajouts sur un même lot,
/// ce qu'un simulacre ligne à ligne décrirait mal. Seules les méthodes utilisées par l'import
/// sont implémentées ; la recherche par SKU valide le code comme le vrai dépôt.
/// </summary>
internal sealed class FauxProduits : IProductRepository
{
    public List<Product> Enregistres { get; } = [];
    public List<Product> Ajoutes { get; } = [];

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        var valeur = Sku.Create(sku);
        return Task.FromResult(Enregistres.FirstOrDefault(p => p.Sku == valeur));
    }

    public Task AddAsync(Product product, CancellationToken ct = default)
    {
        Ajoutes.Add(product);
        return Task.CompletedTask;
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Product>> ListAsync(int maxRows, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Product>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(IReadOnlyList<(Product Product, int TotalQuantity)> Items, int TotalCount)> ListLowStockPagedAsync(
        int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(IReadOnlyList<Product> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize, string? search = null, int? minQuantity = null, int? maxQuantity = null,
        bool lowStockOnly = false, ProductSortBy sortBy = ProductSortBy.Name, bool sortDescending = false,
        string? collection = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CatalogTotals> GetCatalogTotalsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<CollectionCount>> CountByCollectionAsync(int top, CancellationToken ct = default) => throw new NotSupportedException();
    public void Update(Product product) => throw new NotSupportedException();
}

internal sealed class FauxStocks : IStockRepository
{
    public List<Stock> Lignes { get; } = [];

    public Task<Stock?> GetAsync(Guid productId, Guid warehouseId, CancellationToken ct = default)
        => Task.FromResult(Lignes.FirstOrDefault(s => s.ProductId == productId && s.WarehouseId == warehouseId));

    public async Task<Stock> GetOrCreateAsync(Guid productId, Guid warehouseId, CancellationToken ct = default)
    {
        var existant = await GetAsync(productId, warehouseId, ct);
        if (existant is not null)
            return existant;
        var cree = Stock.Create(productId, warehouseId);
        Lignes.Add(cree);
        return cree;
    }

    public Task AddAsync(Stock stock, CancellationToken ct = default)
    {
        if (!Lignes.Contains(stock))
            Lignes.Add(stock);
        return Task.CompletedTask;
    }

    public void Update(Stock stock)
    {
    }

    public Task<IReadOnlyList<Stock>> ListByProductAsync(Guid productId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyDictionary<Guid, int>> GetTotalQuantitiesAsync(IEnumerable<Guid> productIds, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyDictionary<Guid, ProductLocation>> GetPrimaryLocationsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<WarehouseLoad>> GetTotalsByWarehouseAsync(CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class FauxEntrepots : IWarehouseRepository
{
    public List<Warehouse> Existants { get; } = [];
    public List<Warehouse> Ajoutes { get; } = [];

    public Task<IReadOnlyList<Warehouse>> ListAsync(int maxRows, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Warehouse>>(Existants.Take(maxRows).ToList());

    public Task AddAsync(Warehouse warehouse, CancellationToken ct = default)
    {
        Ajoutes.Add(warehouse);
        return Task.CompletedTask;
    }

    public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> IsActiveAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Warehouse>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
    public void Update(Warehouse warehouse) => throw new NotSupportedException();
}

internal sealed class FausseUniteDeTravail : IUnitOfWork
{
    public int Enregistrements { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Enregistrements++;
        return Task.FromResult(0);
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
        => action(ct);
}
