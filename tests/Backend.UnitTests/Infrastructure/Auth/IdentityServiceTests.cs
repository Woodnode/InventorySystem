using FluentAssertions;
using InventorySystem.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Infrastructure.Auth;

/// <summary>
/// Non-régression de l'audit de sécurité (énumération des comptes par le délai de réponse) :
/// une adresse inconnue doit payer une vérification de mot de passe, comme un vrai compte.
/// </summary>
public sealed class IdentityServiceTests
{
    private readonly Mock<IPasswordHasher<ApplicationUser>> _hasher = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<RoleManager<ApplicationRole>> _roleManager;

    public IdentityServiceTests()
    {
        _hasher
            .Setup(h => h.HashPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Returns("hash-factice");
        _hasher
            .Setup(h => h.VerifyHashedPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Failed);

        _userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, _hasher.Object, null!, null!, null!, null!, null!, null!);
        _roleManager = new Mock<RoleManager<ApplicationRole>>(
            new Mock<IRoleStore<ApplicationRole>>().Object, null!, null!, null!, null!);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithUnknownEmail_StillVerifiesAPasswordHash()
    {
        _userManager
            .Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        var sut = new IdentityService(_userManager.Object, _roleManager.Object);

        var result = await sut.ValidateCredentialsAsync("inconnu@test.local", "MotDePasse1!");

        result.Should().BeNull();
        _hasher.Verify(h => h.VerifyHashedPassword(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), "MotDePasse1!"), Times.Once);
    }
}
