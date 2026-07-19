using InventorySystem.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Implémentation de <see cref="IIdentityService"/> via ASP.NET Core Identity
/// (UserManager/RoleManager). Seule classe du projet à référencer ces types (DIP).
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public IdentityService(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IdentityOperationResult> CreateUserAsync(
        string email, string password, string displayName, string role, CancellationToken ct = default)
    {
        if (!await _roleManager.RoleExistsAsync(role))
            return IdentityOperationResult.Failure(new[] { $"Le rôle '{role}' n'existe pas." });

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
            // UserName = Email dans ce projet : un email en double déclenche à la fois
            // DuplicateUserName et DuplicateEmail, avec le même message — Distinct() évite
            // de l'afficher deux fois côté utilisateur.
            return IdentityOperationResult.Failure(
                createResult.Errors.Select(e => e.Description).Distinct().ToList());

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
            return IdentityOperationResult.Failure(roleResult.Errors.Select(e => e.Description).ToList());

        return IdentityOperationResult.Success(user.Id);
    }

    public async Task<IdentityUserInfo?> ValidateCredentialsAsync(
        string email, string password, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return null;

        // Verrouillage après tentatives échouées : géré nativement par UserManager
        // (Options.Lockout, câblé dans DependencyInjection.cs — voir plan §6).
        if (await _userManager.IsLockedOutAsync(user))
            return null;

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return null;
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        return await ToUserInfoAsync(user);
    }

    public async Task<IdentityUserInfo?> FindByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : await ToUserInfoAsync(user);
    }

    private async Task<IdentityUserInfo> ToUserInfoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new IdentityUserInfo(user.Id, user.Email!, user.DisplayName, roles.ToList());
    }
}
