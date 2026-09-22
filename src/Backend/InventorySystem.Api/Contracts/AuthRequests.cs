namespace InventorySystem.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role);

public sealed record RefreshTokenRequest(string RefreshToken);
