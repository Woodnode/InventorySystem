using Microsoft.AspNetCore.Identity;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// ASP.NET Core Identity renvoie ses messages d'erreur en anglais par défaut
/// (IdentityError.Description) — ces messages remontent tels quels jusqu'au frontend
/// (voir RegisterCommandHandler, IdentityService.CreateUserAsync) et s'afficheraient
/// mélangés à une UI entièrement en français. Ne couvre que les erreurs réellement
/// atteignables vu la politique de mot de passe configurée (DependencyInjection.cs) et
/// UserName = Email dans ce projet.
/// </summary>
public sealed class FrenchIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = $"L'adresse '{email}' est déjà utilisée par un compte existant.",
    };

    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = $"L'adresse '{userName}' est déjà utilisée par un compte existant.",
    };

    public override IdentityError InvalidEmail(string? email) => new()
    {
        Code = nameof(InvalidEmail),
        Description = $"'{email}' n'est pas une adresse courriel valide.",
    };

    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"Le mot de passe doit contenir au moins {length} caractères.",
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "Le mot de passe doit contenir au moins un chiffre.",
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "Le mot de passe doit contenir au moins une majuscule.",
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "Le mot de passe doit contenir au moins une minuscule.",
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "Le mot de passe doit contenir au moins un caractère spécial.",
    };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
    {
        Code = nameof(PasswordRequiresUniqueChars),
        Description = $"Le mot de passe doit contenir au moins {uniqueChars} caractères distincts.",
    };

    public override IdentityError PasswordMismatch() => new()
    {
        Code = nameof(PasswordMismatch),
        Description = "Mot de passe incorrect.",
    };

    public override IdentityError InvalidUserName(string? userName) => new()
    {
        Code = nameof(InvalidUserName),
        Description = $"'{userName}' n'est pas un nom d'utilisateur valide.",
    };

    public override IdentityError DefaultError() => new()
    {
        Code = nameof(DefaultError),
        Description = "Une erreur inattendue est survenue.",
    };
}
