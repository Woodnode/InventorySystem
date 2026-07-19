namespace InventorySystem.Application.Common.Interfaces;

/// <summary>
/// Abstraction sur la gestion des utilisateurs et l'authentification par mot de passe.
/// Implémentée dans Infrastructure via ASP.NET Core Identity — l'Application ne référence
/// jamais Microsoft.AspNetCore.Identity directement (DIP, voir plan §3.2/§4).
/// </summary>
public interface IIdentityService
{
    Task<IdentityOperationResult> CreateUserAsync(
        string email, string password, string displayName, string role, CancellationToken ct = default);

    /// <summary>Valide les identifiants et renvoie l'utilisateur (et ses rôles) si valides.</summary>
    Task<IdentityUserInfo?> ValidateCredentialsAsync(
        string email, string password, CancellationToken ct = default);

    Task<IdentityUserInfo?> FindByIdAsync(Guid userId, CancellationToken ct = default);
}

public sealed record IdentityOperationResult(bool Succeeded, Guid? UserId, IReadOnlyList<string> Errors)
{
    public static IdentityOperationResult Success(Guid userId) => new(true, userId, Array.Empty<string>());
    public static IdentityOperationResult Failure(IReadOnlyList<string> errors) => new(false, null, errors);
}

public sealed record IdentityUserInfo(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);
