using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

/// <summary>
/// Controller mince : reçoit la requête, délègue à MediatR, renvoie le résultat — voir
/// InventorySystem.Application.Auth (plan §6). Seule exception : la garde anti-escalade de
/// rôle sur <see cref="Register"/>, qui a besoin de <see cref="ControllerBase.User"/> (l'appelant
/// courant) et ne peut donc pas vivre dans un handler MediatR sans lui faire porter un
/// concept HTTP.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    public AuthController(ISender mediator) => _mediator = mediator;

    /// <summary>
    /// Crée un compte. Ouvert par défaut pour le rôle "Employe" ; créer un compte
    /// Admin/Gestionnaire nécessite d'être déjà authentifié en tant qu'Admin.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Register(RegisterCommand command, CancellationToken ct)
    {
        if (command.Role != "Employe" && !(User.Identity?.IsAuthenticated == true && User.IsInRole("Admin")))
            return Forbid();

        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Login(LoginCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    /// <summary>Renouvelle l'access token à partir d'un refresh token valide (rotation).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Refresh(RefreshTokenCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));
}
