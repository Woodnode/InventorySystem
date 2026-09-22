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
            request.WarehouseId, request.InitialQuantity, request.SupplierId,
            request.ProjectCode, request.Collection, request.VolumeNumber, request.ProductType, request.Year, request.WeightPerCopyLb,
            request.Company,
            request.Section, request.Space, request.Pallet, request.BoxesCount, request.CopiesPerBox,
            request.EntryDate, request.ExitDate, request.DistributorName, request.ReturnDate, request.Comment);
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    /// <summary>
    /// Liste paginée des produits, avec leur stock total agrégé. <paramref name="search"/>
    /// filtre sur SKU ou nom ; <paramref name="minQuantity"/>/<paramref name="maxQuantity"/>/
    /// <paramref name="lowStockOnly"/> filtrent sur le stock total agrégé. <paramref name="sortBy"/>/
    /// <paramref name="sortDescending"/> contrôlent le tri (nom par défaut).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? minQuantity = null,
        [FromQuery] int? maxQuantity = null,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] ProductSortBy sortBy = ProductSortBy.Name,
        [FromQuery] bool sortDescending = false,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetProductsQuery(
            page, pageSize, search, minQuantity, maxQuantity, lowStockOnly, sortBy, sortDescending), ct));

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

    /// <summary>Liste paginée des produits dont le stock est sous le seuil de réappro.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetLowStock(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetLowStockQuery(page, pageSize), ct));

    /// <summary>Exporte le catalogue produits (avec stock total agrégé) en CSV ou Excel.</summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] ExportFormat format, CancellationToken ct)
    {
        var file = await _mediator.Send(new ExportProductsQuery(format), ct);
        if (file.Truncated)
            Response.Headers.Append("X-Export-Truncated", "true");
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Importe le catalogue produits depuis un fichier CSV.</summary>
    [HttpPost("import")]
    [Authorize(Policy = "RequireGestionnaireOrAbove")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Fichier manquant ou vide.");

        using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new ImportProductsCommand(stream, file.FileName), ct);
        return Ok(result);
    }
}
