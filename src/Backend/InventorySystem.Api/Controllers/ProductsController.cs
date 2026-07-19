using InventorySystem.Api.Contracts;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Products.Commands;
using InventorySystem.Application.Products.Dtos;
using InventorySystem.Application.Products.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

/// <summary>
/// Controller mince : reçoit la requête, délègue à MediatR, renvoie le résultat.
/// Aucune logique métier ici (voir plan §3.4). Accès restreint par policies de rôles
/// (voir plan §6) : créer un produit est réservé à Gestionnaire/Admin, la consultation
/// est ouverte à tout utilisateur authentifié (y compris un Employé sur le terrain).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "RequireEmployeOrAbove")]
public sealed class ProductsController : ControllerBase
{
    private readonly ISender _mediator;

    public ProductsController(ISender mediator) => _mediator = mediator;

    /// <summary>Crée un nouveau produit.</summary>
    [HttpPost]
    [Authorize(Policy = "RequireGestionnaireOrAbove")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        var command = new CreateProductCommand(
            request.Sku, request.Name, request.Description, request.LowStockThreshold,
            request.WarehouseId, request.InitialQuantity, request.SupplierId);
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    /// <summary>Liste paginée des produits, avec leur stock total agrégé.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetProductsQuery(page, pageSize), ct));

    /// <summary>Récupère un produit par id, avec son stock total agrégé.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken ct)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id), ct);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Récupère un produit par SKU (recherche exacte — utilisé par le scan mobile).</summary>
    [HttpGet("by-sku/{sku}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetBySku(string sku, CancellationToken ct)
    {
        var product = await _mediator.Send(new GetProductBySkuQuery(sku), ct);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Liste les produits dont le stock est sous le seuil de réappro.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetLowStock(CancellationToken ct)
        => Ok(await _mediator.Send(new GetLowStockQuery(), ct));

    /// <summary>Exporte le catalogue produits (avec stock total agrégé) en CSV ou Excel.</summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] ExportFormat format, CancellationToken ct)
    {
        var file = await _mediator.Send(new ExportProductsQuery(format), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
