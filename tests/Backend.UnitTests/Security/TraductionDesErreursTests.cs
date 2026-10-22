using System.Net;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using InventorySystem.Api.Middlewares;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using AppException = InventorySystem.Application.Common.Exceptions.ApplicationException;

namespace InventorySystem.Backend.UnitTests.Security;

/// <summary>
/// ExceptionHandlingMiddleware : chaque famille d'exception devient un ProblemDetails au bon
/// statut, et une exception imprévue ne laisse rien filtrer de son message ni de sa pile.
/// </summary>
public sealed class TraductionDesErreursTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _logger = new();

    private async Task<(HttpContext Context, ProblemDetails Probleme, string Corps)> ExecuterAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, _logger.Object);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var corps = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var probleme = JsonSerializer.Deserialize<ProblemDetails>(corps, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return (context, probleme, corps);
    }

    public static TheoryData<Exception, HttpStatusCode, string> ExceptionsMetier() => new()
    {
        { new AuthenticationException("Identifiants invalides."), HttpStatusCode.Unauthorized, "Authentification échouée" },
        { new IdentityOperationException("Ce courriel est déjà utilisé."), HttpStatusCode.BadRequest, "Opération Identity échouée" },
        { new ConcurrencyConflictException("Ce SKU est déjà utilisé."), HttpStatusCode.Conflict, "Conflit de concurrence" },
        { new AppException("Opération impossible."), HttpStatusCode.BadRequest, "Erreur applicative" },
        { new DomainException("Le SKU ne peut pas être vide."), HttpStatusCode.BadRequest, "Règle métier violée" },
        { new InsufficientStockException(3, 5), HttpStatusCode.BadRequest, "Règle métier violée" },
        { new InvalidMovementException("Ce mouvement a déjà été synchronisé."), HttpStatusCode.BadRequest, "Règle métier violée" },
    };

    [Theory]
    [MemberData(nameof(ExceptionsMetier))]
    public async Task EtantDonneExceptionConnue_QuandTraduire_AlorsStatutTitreEtMessageMetier(
        Exception exception, HttpStatusCode statut, string titre)
    {
        var (context, probleme, _) = await ExecuterAsync(exception);

        context.Response.StatusCode.Should().Be((int)statut);
        context.Response.ContentType.Should().Be("application/problem+json");
        probleme.Status.Should().Be((int)statut);
        probleme.Title.Should().Be(titre);
        probleme.Detail.Should().Be(exception.Message);
    }

    [Fact]
    public async Task EtantDonneValidationEchouee_QuandTraduire_AlorsTousLesMessagesSansNomDePropriete()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Sku", "Le SKU est obligatoire."),
            new ValidationFailure("Name", "Le nom est obligatoire."),
        ]);

        var (context, probleme, _) = await ExecuterAsync(exception);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        probleme.Title.Should().Be("Erreur de validation");
        probleme.Detail.Should().Be("Le SKU est obligatoire. | Le nom est obligatoire.");
    }

    [Fact]
    public async Task EtantDonneExceptionImprevue_QuandTraduire_AlorsMessageGeneriqueSansFuite()
    {
        var secret = new InvalidOperationException(
            "Npgsql: password authentication failed for user \"inventory\" at Host=db;Password=s3cr3t");

        var (context, probleme, corps) = await ExecuterAsync(secret);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        probleme.Title.Should().Be("Erreur interne");
        probleme.Detail.Should().Be("Une erreur inattendue est survenue.");
        corps.Should().NotContain("Npgsql").And.NotContain("s3cr3t").And.NotContain("InvalidOperationException")
            .And.NotContain(" at ");
    }

    [Fact]
    public async Task EtantDonneExceptionImprevue_QuandTraduire_AlorsJournaliseeEnErreurAvecLExceptionComplete()
    {
        var exception = new NullReferenceException("détail interne");

        await ExecuterAsync(exception);

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            exception,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneEchecDAuthentification_QuandTraduire_AlorsTraceDeSecuriteSansException()
    {
        // Trace d'avertissement (tentatives en série visibles), sans pile : ce n'est pas une panne.
        await ExecuterAsync(new AuthenticationException("Refresh token invalide ou expiré."));

        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Refresh token invalide ou expiré.")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task EtantDonneAucuneException_QuandTraverser_AlorsReponseIntacte()
    {
        var context = new DefaultHttpContext();
        var middleware = new ExceptionHandlingMiddleware(c =>
        {
            c.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }, _logger.Object);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        context.Response.ContentType.Should().BeNull();
    }
}
