using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;

namespace InventoryMobile.Services;

/// <summary>
/// Extrait un message lisible d'une réponse d'erreur API. Le backend renvoie un
/// <c>ProblemDetails</c> (<c>application/problem+json</c>) avec un champ <c>detail</c>
/// contenant déjà le message métier localisé — voir ExceptionHandlingMiddleware côté
/// backend. Pas de couche de traduction supplémentaire nécessaire ici.
/// </summary>
public static class ApiErrorReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static Task<string?> ReadDetailAsync(ApiException exception)
    {
        if (string.IsNullOrWhiteSpace(exception.Content)) return Task.FromResult<string?>(null);

        try
        {
            var problem = JsonSerializer.Deserialize<ProblemDetailsPayload>(exception.Content, Options);
            return Task.FromResult(problem?.Detail);
        }
        catch (JsonException)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed record ProblemDetailsPayload(
        [property: JsonPropertyName("detail")] string? Detail,
        [property: JsonPropertyName("title")] string? Title);
}
