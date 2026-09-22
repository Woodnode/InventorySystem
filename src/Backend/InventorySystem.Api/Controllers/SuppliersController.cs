using InventorySystem.Api.Contracts;
using InventorySystem.Application.Suppliers.Commands;
using InventorySystem.Application.Suppliers.Dtos;
using InventorySystem.Application.Suppliers.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

/// <summary>Controller mince — voir plan §3.4. Création réservée à Gestionnaire/Admin.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "RequireEmployeOrAbove")]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISender _mediator;

    public SuppliersController(ISender mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = "RequireGestionnaireOrAbove")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateSupplierRequest request, CancellationToken ct)
    {
        var command = new CreateSupplierCommand(request.Name, request.ContactEmail, request.Phone);
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetSuppliersQuery(), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireGestionnaireOrAbove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, UpdateSupplierRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateSupplierCommand(id, request.Name, request.ContactEmail, request.Phone), ct);
        return NoContent();
    }

    /// <summary>
    /// Active/désactive plutôt que supprimer : un fournisseur référencé par des produits
    /// existants ne doit jamais disparaître (parité avec WarehousesController — voir ré-audit).
    /// </summary>
    [HttpPatch("{id:guid}/active")]
    [Authorize(Policy = "RequireGestionnaireOrAbove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(Guid id, SetSupplierActiveRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SetSupplierActiveCommand(id, request.IsActive), ct);
        return NoContent();
    }
}
