namespace InventorySystem.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role);

/// <summary>
/// Corps de /auth/refresh et /auth/logout pour le client mobile. Facultatif pour le client web,
/// dont le jeton arrive dans le cookie httpOnly.
/// </summary>
public sealed record RefreshTokenRequest(string? RefreshToken);
