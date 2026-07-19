namespace InventorySystem.Application.Auth.Dtos;

/// <summary>DTO de sortie pour login/register/refresh — jamais l'entité Identity directement.</summary>
public sealed record AuthResultDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);
