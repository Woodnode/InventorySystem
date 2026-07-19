using System.Net;
using FluentValidation;
using InventorySystem.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InventorySystem.Api.Middlewares;

/// <summary>
/// Gestion centralisée des exceptions : traduit les exceptions métier / de validation
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
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Règle métier violée", ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Un autre mouvement a modifié le même stock entre la lecture et l'écriture
            // (jeton de concurrence xmin périmé — voir StockConfiguration). Le client peut
            // réessayer la requête sur l'état à jour.
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "Conflit de concurrence",
                "Le stock a été modifié entre-temps par une autre opération. Veuillez réessayer.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Deux requêtes concurrentes ont chacune tenté de créer la ligne de stock initiale
            // pour le même (ProductId, WarehouseId) — voir StockRepository.GetOrCreateAsync,
            // qui vérifie "existe déjà ?" puis insère sans verrou. La seconde insertion viole
            // l'index unique de StockConfiguration ; le client peut réessayer sur l'état à jour.
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "Conflit de concurrence",
                "Le stock a déjà été initialisé entre-temps par une autre opération. Veuillez réessayer.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur non gérée");
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError,
                "Erreur interne", "Une erreur inattendue est survenue.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

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
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
