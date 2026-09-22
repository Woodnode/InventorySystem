using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
        => _db.Products.FirstOrDefaultAsync(p => p.Sku == Sku.Create(sku), ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Products.AnyAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> ListAsync(int maxRows, CancellationToken ct = default)
        => await _db.Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .Take(maxRows)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await _db.Products.AsNoTracking()
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<(Product Product, int TotalQuantity)> Items, int TotalCount)> ListLowStockPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query =
            from p in _db.Products.AsNoTracking()
            join s in _db.Stocks.AsNoTracking() on p.Id equals s.ProductId into stocks
            let total = stocks.Sum(x => (int?)x.Quantity) ?? 0
            where total <= p.LowStockThreshold
            orderby p.Name
            select new { Product = p, TotalQuantity = total };

        var totalCount = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (rows.Select(r => (r.Product, r.TotalQuantity)).ToList(), totalCount);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize, string? search = null, int? minQuantity = null, int? maxQuantity = null,
        bool lowStockOnly = false, ProductSortBy sortBy = ProductSortBy.Name, bool sortDescending = false,
        CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            // Sku est un Value Object converti (record Sku <-> colonne texte "sku") : EF Core
            // ne sait pas traduire l'accès à .Sku.Value dans une clause ILIKE (le converter ne
            // couvre que la propriété entière, pas ses membres). On résout donc les ids
            // correspondants via une requête SQL brute avant de filtrer le catalogue.
            var matchingIds = await _db.Database.SqlQuery<Guid>(
                $"SELECT \"Id\" FROM products WHERE sku ILIKE {pattern} OR \"Name\" ILIKE {pattern}"
            ).ToListAsync(ct);
            query = query.Where(p => matchingIds.Contains(p.Id));
        }

        // La quantité n'est pas portée par Product (voir Stock) : filtrer ou trier dessus
        // nécessite une jointure. On ne la paie que si l'un de ces critères la demande.
        if (minQuantity.HasValue || maxQuantity.HasValue || lowStockOnly || sortBy == ProductSortBy.Quantity)
        {
            var withStock =
                from p in query
                join s in _db.Stocks.AsNoTracking() on p.Id equals s.ProductId into stocks
                let total = stocks.Sum(x => (int?)x.Quantity) ?? 0
                select new { Product = p, TotalQuantity = total };

            if (minQuantity.HasValue)
                withStock = withStock.Where(x => x.TotalQuantity >= minQuantity.Value);
            if (maxQuantity.HasValue)
                withStock = withStock.Where(x => x.TotalQuantity <= maxQuantity.Value);
            if (lowStockOnly)
                withStock = withStock.Where(x => x.TotalQuantity <= x.Product.LowStockThreshold);

            var ordered = sortBy switch
            {
                ProductSortBy.Sku => sortDescending ? withStock.OrderByDescending(x => x.Product.Sku) : withStock.OrderBy(x => x.Product.Sku),
                ProductSortBy.Quantity => sortDescending ? withStock.OrderByDescending(x => x.TotalQuantity) : withStock.OrderBy(x => x.TotalQuantity),
                _ => sortDescending ? withStock.OrderByDescending(x => x.Product.Name) : withStock.OrderBy(x => x.Product.Name),
            };

            var filteredTotalCount = await ordered.CountAsync(ct);
            var page1 = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Product).ToListAsync(ct);
            return (page1, filteredTotalCount);
        }
        else
        {
            var ordered = sortBy switch
            {
                ProductSortBy.Sku => sortDescending ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
                _ => sortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            };

            var totalCount = await ordered.CountAsync(ct);
            var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return (items, totalCount);
        }
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await _db.Products.AddAsync(product, ct);

    public void Update(Product product) => _db.Products.Update(product);
}
