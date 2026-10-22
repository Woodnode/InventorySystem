using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using InventorySystem.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventorySystem.Backend.UnitTests.Security;

/// <summary>
/// TokenService : refus d'une clé de signature absente ou trop courte, et contenu des jetons
/// émis (signature, émetteur, audience, durée, rôles, refresh token opaque et haché).
/// </summary>
public sealed class ValidationCleJwtTests
{
    private const string CleValide = "cle-de-test-unitaire-assez-longue-pour-hs256";

    private static TokenService Creer(string? key, int accessMinutes = 15, int refreshDays = 7)
        => new(Options.Create(new JwtOptions
        {
            Issuer = "InventorySystem",
            Audience = "InventorySystemClients",
            Key = key!,
            AccessTokenMinutes = accessMinutes,
            RefreshTokenDays = refreshDays,
        }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cle-de-31-caracteres-trop-court")]
    public void EtantDonneCleAbsenteOuTropCourte_QuandCreerLeService_AlorsDemarrageRefuse(string? key)
    {
        var act = () => Creer(key);

        act.Should().Throw<InvalidOperationException>().WithMessage("*32 caractères minimum*");
    }

    [Fact]
    public void EtantDonneCleDeTrenteDeuxCaracteres_QuandCreerLeService_AlorsAcceptee()
    {
        var act = () => Creer(new string('k', 32));

        act.Should().NotThrow();
    }

    [Fact]
    public void EtantDonneCleRefusee_QuandLireLeMessage_AlorsLaCleNApparaitPas()
    {
        const string fuite = "cle-secrete-courte";

        var act = () => Creer(fuite);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain(fuite);
    }

    [Fact]
    public void EtantDonneJetonEmis_QuandLeValiderAvecLaMemeCle_AlorsSignatureEmetteurEtAudienceConformes()
    {
        var service = Creer(CleValide);
        var userId = Guid.NewGuid();

        var jeton = service.GenerateAccessToken(userId, "gaby@test.local", ["Gestionnaire"]);

        var principal = new JwtSecurityTokenHandler().ValidateToken(jeton.Token, new TokenValidationParameters
        {
            ValidIssuer = "InventorySystem",
            ValidAudience = "InventorySystemClients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CleValide)),
            ClockSkew = TimeSpan.Zero,
        }, out var valide);
        ((JwtSecurityToken)valide).Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(userId.ToString());
        principal.IsInRole("Gestionnaire").Should().BeTrue();
        principal.IsInRole("Admin").Should().BeFalse();
    }

    [Fact]
    public void EtantDonneJetonEmis_QuandLeValiderAvecUneAutreCle_AlorsSignatureRejetee()
    {
        var jeton = Creer(CleValide).GenerateAccessToken(Guid.NewGuid(), "a@test.local", ["Admin"]);

        var act = () => new JwtSecurityTokenHandler().ValidateToken(jeton.Token, new TokenValidationParameters
        {
            ValidIssuer = "InventorySystem",
            ValidAudience = "InventorySystemClients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("une-autre-cle-tout-aussi-longue-que-la-vraie")),
        }, out _);

        act.Should().Throw<SecurityTokenSignatureKeyNotFoundException>();
    }

    [Fact]
    public void EtantDonneDureeConfiguree_QuandEmettreUnJeton_AlorsExpirationAlignee()
    {
        var avant = DateTime.UtcNow;

        var jeton = Creer(CleValide, accessMinutes: 5).GenerateAccessToken(Guid.NewGuid(), "a@test.local", []);

        jeton.ExpiresAtUtc.Should().BeOnOrAfter(avant.AddMinutes(5)).And.BeOnOrBefore(DateTime.UtcNow.AddMinutes(5));
        new JwtSecurityTokenHandler().ReadJwtToken(jeton.Token).ValidTo
            .Should().BeCloseTo(jeton.ExpiresAtUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void EtantDonneDeuxJetonsPourLeMemeUtilisateur_QuandLesComparer_AlorsIdentifiantsUniques()
    {
        var service = Creer(CleValide);
        var userId = Guid.NewGuid();

        var premier = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateAccessToken(userId, "a@test.local", []).Token);
        var second = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateAccessToken(userId, "a@test.local", []).Token);

        premier.Id.Should().NotBeNullOrEmpty().And.NotBe(second.Id);
    }

    [Fact]
    public void EtantDonneJetonEmis_QuandLireSonContenu_AlorsAucunSecretNiRefreshToken()
    {
        var service = Creer(CleValide);

        var jeton = new JwtSecurityTokenHandler().ReadJwtToken(
            service.GenerateAccessToken(Guid.NewGuid(), "a@test.local", ["Employe"]).Token);

        jeton.Claims.Select(c => c.Type).Should().BeEquivalentTo(
            ["sub", "email", "jti", ClaimTypes.Role, "exp", "iss", "aud"],
            "le jeton est lisible par quiconque le détient : rien d'autre ne doit y figurer");
    }

    [Fact]
    public void EtantDonneRefreshTokens_QuandEnGenererPlusieurs_AlorsOpaquesLongsEtDistincts()
    {
        var service = Creer(CleValide);

        var jetons = Enumerable.Range(0, 50).Select(_ => service.GenerateRefreshToken()).ToList();

        jetons.Should().OnlyHaveUniqueItems();
        jetons.Should().OnlyContain(j => Convert.FromBase64String(j).Length == 64);
        jetons.Should().OnlyContain(j => !j.Contains('.'), "un refresh token n'est pas un JWT");
    }

    [Fact]
    public void EtantDonneRefreshToken_QuandLeHacher_AlorsSha256HexadecimalDeterministe()
    {
        var service = Creer(CleValide);

        var hash = service.HashRefreshToken("jeton-de-test");

        hash.Should().Be(service.HashRefreshToken("jeton-de-test"));
        hash.Should().MatchRegex("^[0-9A-F]{64}$");
        hash.Should().NotBe(service.HashRefreshToken("jeton-de-test2"));
    }

    [Fact]
    public void EtantDonneDureeDeRefreshConfiguree_QuandCalculerLExpiration_AlorsEnJours()
    {
        var expiration = Creer(CleValide, refreshDays: 3).GetRefreshTokenExpiryUtc();

        expiration.Should().BeCloseTo(DateTime.UtcNow.AddDays(3), TimeSpan.FromMinutes(1));
        expiration.Kind.Should().Be(DateTimeKind.Utc);
    }
}
