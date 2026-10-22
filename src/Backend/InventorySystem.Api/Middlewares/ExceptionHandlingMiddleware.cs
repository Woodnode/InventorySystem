using System.Net;
using FluentValidation;
using InventorySystem.Api.Security;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using AppException = InventorySystem.Application.Common.Exceptions.ApplicationException;

namespace InventorySystem.Api.Middlewares;

/// <summary>
/// Gestion centralisée des exceptions : traduit Domain / Application / validation
/// en réponses ProblemDetails normalisées (voir plan §3.4).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Erreur de validation",
                string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (AuthenticationException ex)
        {
            // Trace de sécurité : échecs de connexion, jetons refusés, réutilisation d'un refresh
            // token (révocation de la session). Le message ne contient ni courriel ni jeton.
            _logger.LogWarning("Authentification refusée sur {Path} depuis {ClientIp} : {Reason}",
                context.Request.Path, ClientIpForwarding.PartitionKey(context), ex.Message);
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Authentification échouée", ex.Message);
        }
        catch (IdentityOperationException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Opération Identity échouée", ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // Traduite depuis EF Core / Npgsql dans AppDbContext.SaveChangesAsync (Infrastructure) :
            // ce middleware Api n'a pas besoin de connaître EF/Npgsql (voir AUDIT.md B-CA1).
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "Conflit de concurrence", ex.Message);
        }
        catch (AppException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Erreur applicative", ex.Message);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Règle métier violée", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur non gérée");
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError,
                "Erreur interne", "Une erreur inattendue est survenue.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context, HttpStatusCode status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail
        };

        context.Response.StatusCode = (int)status;
        // Type passé à WriteAsJsonAsync : posé à part, il était remplacé par application/json.
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
