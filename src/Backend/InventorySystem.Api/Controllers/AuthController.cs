using InventorySystem.Api.Auth;
using InventorySystem.Api.Contracts;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InventorySystem.Api.Controllers;

/// <summary>
/// Controller mince : reçoit la requête HTTP, mappe vers une command MediatR, renvoie le résultat.
/// Le seul travail propre à HTTP est le transport du refresh token : cookie httpOnly pour le
/// client web, corps JSON pour le client mobile (voir <see cref="RefreshTokenCookie"/>).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IConfiguration _configuration;

    public AuthController(ISender mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    /// <summary>
    /// Crée un compte. L'inscription libre (rôle "Employe" sans être connecté) dépend du réglage
    /// "Registration:PublicEnabled", fermé sur le site vitrine ; la création par un Admin connecté
    /// reste toujours possible.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

        if (request.Role != "Employe" && !isAdmin)
            return Forbid();

        // Inscription libre fermée sur le site vitrine : le compte de démonstration suffit à
        // visiter l'application, et personne ne peut créer de comptes sur le serveur.
        // La création par un Admin connecté reste possible.
        if (!isAdmin && !_configuration.GetValue<bool>("Registration:PublicEnabled"))
            return NotFound();

        var command = new RegisterCommand(request.Email, request.Password, request.DisplayName, request.Role);
        var result = await _mediator.Send(command, ct);

        // Un Admin qui crée un compte ne doit pas voir sa propre session remplacée par celle
        // du nouvel utilisateur : le cookie n'est posé que pour une inscription libre.
        return Ok(!isAdmin && RefreshTokenCookie.IsRequestedBy(Request) ? MoveRefreshTokenToCookie(result) : result);
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
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password), ct);
        return Ok(RefreshTokenCookie.IsRequestedBy(Request) ? MoveRefreshTokenToCookie(result) : result);
    }

    /// <summary>
    /// Renouvelle l'access token à partir d'un refresh token valide (rotation).
    /// Le jeton est lu d'abord dans le cookie (client web), sinon dans le corps (client mobile),
    /// et le nouveau jeton repart par le même canal. 401 : token absent/invalide/expiré/réutilisé.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken ct)
    {
        var cookieToken = RefreshTokenCookie.Read(Request);
        var refreshToken = cookieToken ?? request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new AuthenticationException("Refresh token manquant.");

        AuthResultDto result;
        try
        {
            result = await _mediator.Send(new RefreshTokenCommand(refreshToken), ct);
        }
        catch (AuthenticationException) when (cookieToken is not null)
        {
            // Jeton révoqué ou expiré : inutile que le navigateur le renvoie encore.
            RefreshTokenCookie.Delete(Response);
            throw;
        }

        return Ok(cookieToken is not null ? MoveRefreshTokenToCookie(result) : result);
    }

    /// <summary>
    /// Ferme la session : révoque la famille du refresh token (cookie, sinon corps) et efface
    /// le cookie. Anonyme, car l'access token peut déjà avoir expiré ; toujours 204.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken ct)
    {
        await _mediator.Send(new LogoutCommand(RefreshTokenCookie.Read(Request) ?? request?.RefreshToken), ct);
        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    /// <summary>Pose le refresh token en cookie et le retire du corps, que le JavaScript ne le voie jamais.</summary>
    private AuthResultDto MoveRefreshTokenToCookie(AuthResultDto result)
    {
        RefreshTokenCookie.Append(Response, result.RefreshToken!, result.RefreshTokenExpiresAtUtc);
        return result with { RefreshToken = null };
    }
}
