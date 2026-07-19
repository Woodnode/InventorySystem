using InventorySystem.Application.Stocks.Dtos;
using InventorySystem.Application.Stocks.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

/// <summary>Controller mince — lecture seule (les mutations passent par MovementsController).</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "RequireEmployeOrAbove")]
public sealed class StocksController : ControllerBase
{
    private readonly ISender _mediator;

    public StocksController(ISender mediator) => _mediator = mediator;

    /// <summary>Répartition du stock d'un produit, entrepôt par entrepôt.</summary>
    [HttpGet("product/{productId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<StockDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockDto>>> GetByProduct(Guid productId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetStockByProductQuery(productId), ct));
}
