using System.Text.Json.Serialization;

namespace InventorySystem.Application.Auth.Dtos;

/// <summary>
/// DTO de sortie pour login/register/refresh — jamais l'entité Identity directement.
/// <para>
/// <see cref="RefreshToken"/> est omis du JSON quand il vaut null : le client web le reçoit
/// uniquement dans un cookie httpOnly (voir AuthController), seul le client mobile le lit
/// dans le corps. <see cref="RefreshTokenExpiresAtUtc"/> ne sort jamais en JSON : il sert
/// à aligner l'expiration du cookie sur celle du jeton.
/// </para>
/// </summary>
public sealed record AuthResultDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RefreshToken,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    [property: JsonIgnore] DateTime RefreshTokenExpiresAtUtc = default);
