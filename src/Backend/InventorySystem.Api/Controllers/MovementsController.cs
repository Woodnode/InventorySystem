using InventorySystem.Api.Contracts;
using InventorySystem.Application.Common.Dtos;
using InventorySystem.Application.Movements.Commands;
using InventorySystem.Application.Movements.Dtos;
using InventorySystem.Application.Movements.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

/// <summary>
/// Controller mince : reçoit la requête, délègue à MediatR, renvoie le résultat.
/// Consommé à l'identique par le frontend React et l'app mobile MAUI (plan §9).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "RequireEmployeOrAbove")] // tout utilisateur authentifié, y compris un employé mobile
public sealed class MovementsController : ControllerBase
{
    private readonly ISender _mediator;

    public MovementsController(ISender mediator) => _mediator = mediator;

    /// <summary>Enregistre un mouvement de stock (entrée/sortie).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Record(RecordMovementRequest request, CancellationToken ct)
    {
        var command = new RecordMovementCommand(
            request.ProductId, request.WarehouseId, request.Type, request.Quantity,
            request.Reason, request.ToWarehouseId, request.ClientGuid);
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetByProduct), new { productId = command.ProductId }, new { id });
    }

    /// <summary>Historique paginé des mouvements d'un produit, plus récents en premier.</summary>
    [HttpGet("product/{productId:guid}")]
    [ProducesResponseType(typeof(PagedResult<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> GetByProduct(
        Guid productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetMovementsByProductQuery(productId, page, pageSize), ct));

    /// <summary>
    /// Exporte l'historique des mouvements en CSV ou Excel — pour un produit donné
    /// (<paramref name="productId"/> renseigné) ou l'historique complet sinon.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] ExportFormat format, [FromQuery] Guid? productId, CancellationToken ct)
    {
        var file = await _mediator.Send(new ExportMovementsQuery(format, productId), ct);
        if (file.Truncated)
            Response.Headers.Append("X-Export-Truncated", "true");
        return File(file.Content, file.ContentType, file.FileName);
    }
}
