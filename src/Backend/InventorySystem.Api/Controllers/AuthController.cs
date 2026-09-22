using InventorySystem.Api.Contracts;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

/// <summary>
/// Controller mince : reçoit la requête HTTP, mappe vers une command MediatR, renvoie le résultat.
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        if (request.Role != "Employe" && !(User.Identity?.IsAuthenticated == true && User.IsInRole("Admin")))
            return Forbid();

        var command = new RegisterCommand(request.Email, request.Password, request.DisplayName, request.Role);
        return Ok(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// 400 : requête malformée (validation FluentValidation, ex. champ vide).
    /// 401 : identifiants invalides (voir <see cref="Application.Common.Exceptions.AuthenticationException"/>,
    /// mappée par <c>ExceptionHandlingMiddleware</c>) — pas 400, pour rester sémantiquement correct
    /// et cohérent avec les clients (voir B-R1/B-R2).
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new LoginCommand(request.Email, request.Password), ct));

    /// <summary>
    /// Renouvelle l'access token à partir d'un refresh token valide (rotation).
    /// 400 : requête malformée (token vide). 401 : token invalide/expiré/réutilisé.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Refresh(RefreshTokenRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct));
}
