using InventorySystem.Application.Dashboard.Dtos;
using InventorySystem.Application.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

/// <summary>
/// Agregats de l'ecran d'accueil. Lecture seule, ouverte a tout utilisateur
/// authentifie : un Employe doit voir l'etat du stock pour savoir quoi reapprovisionner.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "RequireEmployeOrAbove")]
public sealed class DashboardController : ControllerBase
{
    private readonly ISender _mediator;

    public DashboardController(ISender mediator) => _mediator = mediator;

    /// <summary>Totaux du catalogue et flux de stock sur une fenetre glissante.</summary>
    /// <param name="periodDays">Longueur de la fenetre en jours (7 a 180, defaut 30).</param>
    /// <response code="200">Agregats calcules.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardStatsDto>> GetStats(
        [FromQuery] int periodDays = 30, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetDashboardStatsQuery(periodDays), ct));
}
