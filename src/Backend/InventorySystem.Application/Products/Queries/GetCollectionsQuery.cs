using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Application.Products.Dtos;
using MediatR;

namespace InventorySystem.Application.Products.Queries;

/// <summary>
/// Collections éditoriales présentes au catalogue, avec leur nombre de références.
///
/// Alimente le filtre de l'écran Produits : sans cette liste, retrouver une collection
/// supposait d'en connaître le nom exact et de le saisir dans la recherche.
/// </summary>
public sealed record GetCollectionsQuery(int Top = 50) : IRequest<IReadOnlyList<CollectionDto>>;

public sealed class GetCollectionsQueryHandler
    : IRequestHandler<GetCollectionsQuery, IReadOnlyList<CollectionDto>>
{
    private readonly IProductRepository _products;

    public GetCollectionsQueryHandler(IProductRepository products) => _products = products;

    public async Task<IReadOnlyList<CollectionDto>> Handle(
        GetCollectionsQuery request, CancellationToken cancellationToken)
    {
        var top = Math.Clamp(request.Top, 1, 200);
        var lignes = await _products.CountByCollectionAsync(top, cancellationToken);
        return lignes.Select(l => new CollectionDto(l.Collection, l.Count)).ToList();
    }
}
