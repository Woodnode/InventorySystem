using FluentAssertions;
using InventorySystem.Application.Auth.Commands;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Application.Common.Interfaces;
using Moq;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Auth;

/// <summary>
/// Couvre l'atomicité création de compte + émission du premier refresh token (B-S1,
/// corrigé dans <see cref="RegisterCommand"/> via <c>IUnitOfWork.ExecuteInTransactionAsync</c>).
/// </summary>
public sealed class RegisterCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Guid _userId = Guid.NewGuid();

    public RegisterCommandHandlerTests()
    {
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns("raw-refresh-token");
        _tokens.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns("hashed-refresh-token");
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns(new GeneratedAccessToken("access-token", DateTime.UtcNow.AddMinutes(15)));
        _tokens.Setup(t => t.GetRefreshTokenExpiryUtc()).Returns(DateTime.UtcNow.AddDays(7));

        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));
    }

    private RegisterCommandHandler CreateHandler()
        => new(_identity.Object, _tokens.Object, _refreshTokens.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_WithValidData_CreatesUserThenIssuesTokensInOneTransaction()
    {
        _identity
            .Setup(i => i.CreateUserAsync("user@test.local", "Test1234!", "Jean Dupont", "Employe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success(_userId));
        _identity
            .Setup(i => i.FindByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUserInfo(_userId, "user@test.local", "Jean Dupont", new[] { "Employe" }));

        var command = new RegisterCommand("user@test.local", "Test1234!", "Jean Dupont", "Employe");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        // Les deux étapes doivent partager la même frontière transactionnelle.
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokens.Verify(r => r.StoreAsync(
            _userId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<Guid>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenIdentityCreationFails_ThrowsAndNeverIssuesTokens()
    {
        _identity
            .Setup(i => i.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Failure(new[] { "Courriel déjà utilisé." }));

        var command = new RegisterCommand("dup@test.local", "Test1234!", "Jean Dupont", "Employe");
        var act = async () => await CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<IdentityOperationException>();
        _refreshTokens.Verify(r => r.StoreAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenIssuanceFailsAfterUserCreation_ExceptionPropagatesOutOfTheSameTransaction()
    {
        // B-S1 : si l'émission du token échoue APRÈS que le compte a été créé, l'exception
        // doit remonter (et donc annuler toute la transaction côté Infrastructure) au lieu
        // d'être avalée — sinon on aurait un utilisateur "orphelin" persisté sans faute.
        _identity
            .Setup(i => i.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success(_userId));
        _identity
            .Setup(i => i.FindByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityUserInfo(_userId, "user@test.local", "Jean Dupont", new[] { "Employe" }));
        _refreshTokens
            .Setup(r => r.StoreAsync(
                _userId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<Guid>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Échec simulé d'écriture du refresh token."));

        var command = new RegisterCommand("user@test.local", "Test1234!", "Jean Dupont", "Employe");
        var act = async () => await CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // Une seule frontière transactionnelle englobant les deux étapes : c'est elle (côté
        // Infrastructure, hors périmètre de ce test unitaire) qui garantit qu'aucune des deux
        // écritures n'est persistée si l'une échoue.
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
