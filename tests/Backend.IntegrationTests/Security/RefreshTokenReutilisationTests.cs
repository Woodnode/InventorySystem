using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySystem.Backend.IntegrationTests.Security;

/// <summary>
/// Détection du vol de refresh token. Un jeton déjà consommé qui revient hors de la fenêtre
/// de tolérance (30 s) signale une copie volée : toute la famille (la connexion d'origine et
/// ses rotations) est révoquée. Pour rester déterministe, le test ne patiente pas 30 s : il
/// recule en base la date de consommation et vide le cache de rotation, ce que le temps ferait.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class RefreshTokenReutilisationTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RefreshTokenReutilisationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<HttpResponseMessage> RafraichirAsync(string refreshToken)
        => _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

    private async Task<AuthResultDto> RafraichirAvecSuccesAsync(string refreshToken)
    {
        var response = await RafraichirAsync(refreshToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResultDto>())!;
    }

    /// <summary>Fait comme si la consommation du jeton datait de <paramref name="age"/>.</summary>
    private async Task VieillirAsync(string refreshToken, TimeSpan age)
    {
        using var scope = _factory.Services.CreateScope();
        var hash = scope.ServiceProvider.GetRequiredService<ITokenService>().HashRefreshToken(refreshToken);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var revoqueLe = DateTime.UtcNow - age;
        var modifies = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, revoqueLe));
        modifies.Should().Be(1);
        scope.ServiceProvider.GetRequiredService<IRefreshRotationCache>().Remove(hash);
    }

    private async Task<int> JetonsActifsDeLaFamilleAsync(string refreshToken)
    {
        using var scope = _factory.Services.CreateScope();
        var hash = scope.ServiceProvider.GetRequiredService<ITokenService>().HashRefreshToken(refreshToken);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var famille = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => t.FamilyId).SingleAsync();
        return await db.RefreshTokens.CountAsync(t => t.FamilyId == famille && t.RevokedAtUtc == null);
    }

    [Fact]
    public async Task EtantDonneJetonConsommeIlYACinqMinutes_QuandLeRejouer_AlorsFamilleEntiereRevoquee()
    {
        var connexion = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);
        var rotation = await RafraichirAvecSuccesAsync(connexion.RefreshToken!);
        await VieillirAsync(connexion.RefreshToken!, TimeSpan.FromMinutes(5));

        var rejeu = await RafraichirAsync(connexion.RefreshToken!);

        rejeu.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await rejeu.Content.ReadAsStringAsync()).Should().Contain("réutilisé");
        (await RafraichirAsync(rotation.RefreshToken!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "le jeton légitime issu de la rotation appartient à la famille compromise");
        (await JetonsActifsDeLaFamilleAsync(connexion.RefreshToken!)).Should().Be(0);
    }

    [Fact]
    public async Task EtantDonneRejeuHorsFenetre_QuandRevoquer_AlorsUneAutreConnexionDuMemeCompteResteOuverte()
    {
        // Deux appareils du même utilisateur : seule la connexion dont le jeton a fui tombe.
        var appareilA = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);
        var appareilB = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = appareilA.Email, password = RoleTokens.Password });
        var sessionB = (await appareilB.Content.ReadFromJsonAsync<AuthResultDto>())!;
        await RafraichirAvecSuccesAsync(appareilA.RefreshToken!);
        await VieillirAsync(appareilA.RefreshToken!, TimeSpan.FromMinutes(5));

        (await RafraichirAsync(appareilA.RefreshToken!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await RafraichirAsync(sessionB.RefreshToken!)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EtantDonneRejeuDansLaFenetreDeTolerance_QuandLeCacheEstVide_AlorsRefuseSansRevoquerLaFamille()
    {
        // Ex. deux onglets qui rafraîchissent en même temps sur deux instances : le perdant est
        // refusé, mais ce n'est pas un vol, et la session du gagnant continue.
        var connexion = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);
        var rotation = await RafraichirAvecSuccesAsync(connexion.RefreshToken!);
        await VieillirAsync(connexion.RefreshToken!, TimeSpan.FromSeconds(5));

        var rejeu = await RafraichirAsync(connexion.RefreshToken!);

        rejeu.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await rejeu.Content.ReadAsStringAsync()).Should().NotContain("réutilisé");
        (await RafraichirAsync(rotation.RefreshToken!)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EtantDonneJetonExpire_QuandRafraichir_AlorsRefuseAvecLeMessageGenerique()
    {
        var connexion = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);
        using (var scope = _factory.Services.CreateScope())
        {
            var hash = scope.ServiceProvider.GetRequiredService<ITokenService>().HashRefreshToken(connexion.RefreshToken!);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens
                .Where(t => t.TokenHash == hash)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        }

        var response = await RafraichirAsync(connexion.RefreshToken!);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalide ou expiré");
    }

    [Fact]
    public async Task EtantDonneChaineDeRotations_QuandRejouerUnJetonIntermediaire_AlorsLeDernierEstAussiRevoque()
    {
        var connexion = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);
        var deuxieme = await RafraichirAvecSuccesAsync(connexion.RefreshToken!);
        var troisieme = await RafraichirAvecSuccesAsync(deuxieme.RefreshToken!);
        await VieillirAsync(deuxieme.RefreshToken!, TimeSpan.FromMinutes(10));

        (await RafraichirAsync(deuxieme.RefreshToken!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await RafraichirAsync(troisieme.RefreshToken!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneRefreshTokenEnBase_QuandLireLaTable_AlorsSeulLeHacheEstStocke()
    {
        var connexion = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RefreshTokens.AnyAsync(t => t.TokenHash == connexion.RefreshToken)).Should().BeFalse(
            "une fuite de la base ne doit pas livrer de jetons utilisables");
        var hash = scope.ServiceProvider.GetRequiredService<ITokenService>().HashRefreshToken(connexion.RefreshToken!);
        (await db.RefreshTokens.AnyAsync(t => t.TokenHash == hash)).Should().BeTrue();
    }

    [Fact]
    public async Task EtantDonneAccessTokenPresenteCommeRefreshToken_QuandRafraichir_AlorsRefuse()
    {
        var connexion = await RoleTokens.CreateUserAndLoginAsync(_factory, RoleTokens.Employe);

        var response = await RafraichirAsync(connexion.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
