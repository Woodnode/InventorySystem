using InventorySystem.Api.Contracts;
using InventorySystem.Application.Warehouses.Commands;
using InventorySystem.Application.Warehouses.Dtos;
using InventorySystem.Application.Warehouses.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

/// <summary>
/// Controller mince : reçoit la requête, délègue à MediatR, renvoie le résultat (plan §3.4).
/// Consultation ouverte à tout utilisateur authentifié (un Employé doit savoir où sont les
/// entrepôts pour enregistrer un mouvement) ; création réservée à Gestionnaire/Admin.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "RequireEmployeOrAbove")]
public sealed class WarehousesController : ControllerBase
{
    private readonly ISender _mediator;

    public WarehousesController(ISender mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = "RequireGestionnaireOrAbove")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateWarehouseRequest request, CancellationToken ct)
    {
        var command = new CreateWarehouseCommand(request.Name, request.Address);
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetWarehousesQuery(), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireGestionnaireOrAbove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, UpdateWarehouseRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateWarehouseCommand(id, request.Name, request.Address), ct);
        return NoContent();
    }

    /// <summary>
    /// Active/désactive plutôt que supprimer : un entrepôt référencé par du stock ou des
    /// mouvements historiques ne doit jamais disparaître (voir AUDIT.md F-4).
    /// </summary>
    [HttpPatch("{id:guid}/active")]
    [Authorize(Policy = "RequireGestionnaireOrAbove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(Guid id, SetWarehouseActiveRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SetWarehouseActiveCommand(id, request.IsActive), ct);
        return NoContent();
    }
}
