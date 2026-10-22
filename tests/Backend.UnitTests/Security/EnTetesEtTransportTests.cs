using System.Net;
using FluentAssertions;
using InventorySystem.Api.Auth;
using InventorySystem.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InventorySystem.Backend.UnitTests.Security;

/// <summary>
/// Briques de sécurité du transport : en-têtes de réponse, cookie du refresh token, et
/// confiance accordée à l'adresse IP transmise par le relais.
/// </summary>
public sealed class EnTetesEtTransportTests
{
    [Fact]
    public async Task EtantDonneReponse_QuandTraverserLeMiddleware_AlorsCinqEnTetesPosesPuisSuite()
    {
        var context = new DefaultHttpContext();
        var suiteAppelee = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            suiteAppelee = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        suiteAppelee.Should().BeTrue();
        var headers = context.Response.Headers;
        headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        headers.XFrameOptions.ToString().Should().Be("DENY");
        headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
        headers["Permissions-Policy"].ToString().Should().Be("geolocation=(), microphone=(), camera=()");
        headers.ContentSecurityPolicy.ToString().Should().Be("default-src 'none'; frame-ancestors 'none'");
    }

    [Fact]
    public async Task EtantDonneEnTeteDejaPoseParUnClientMalveillant_QuandTraverser_AlorsValeurRemplacee()
    {
        var context = new DefaultHttpContext();
        context.Response.Headers.XFrameOptions = "ALLOWALL";

        await new SecurityHeadersMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.Response.Headers.XFrameOptions.ToString().Should().Be("DENY");
    }

    [Fact]
    public void EtantDonneCookieDuRefreshToken_QuandLireSesOptions_AlorsInaccessibleAuJavaScriptEtLimiteALAuthentification()
    {
        var options = RefreshTokenCookie.BuildOptions(DateTimeOffset.UtcNow.AddDays(7));

        options.HttpOnly.Should().BeTrue();
        options.Secure.Should().BeTrue();
        options.SameSite.Should().Be(SameSiteMode.Strict);
        options.Path.Should().Be("/api/v1/auth");
        options.Domain.Should().BeNull("un cookie sans Domain n'est pas envoyé aux sous-domaines");
    }

    [Theory]
    [InlineData("cookie", true)]
    [InlineData("COOKIE", true)]
    [InlineData("body", false)]
    [InlineData("", false)]
    public void EtantDonneEnTeteDeTransport_QuandLeLire_AlorsCookieSeulementSurDemandeExplicite(string valeur, bool attendu)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[RefreshTokenCookie.TransportHeader] = valeur;

        RefreshTokenCookie.IsRequestedBy(context.Request).Should().Be(attendu);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EtantDonneCookieVide_QuandLeLire_AlorsAucunJeton(string valeur)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{RefreshTokenCookie.Name}={valeur}";

        RefreshTokenCookie.Read(context.Request).Should().BeNull();
    }

    [Fact]
    public void EtantDonneSuppressionDuCookie_QuandLireLEnTete_AlorsMemesAttributsQuALaPose()
    {
        // Sans le même chemin et les mêmes attributs, le navigateur garderait l'ancien cookie.
        var context = new DefaultHttpContext();

        RefreshTokenCookie.Delete(context.Response);

        var setCookie = context.Response.Headers.SetCookie.ToString().ToLowerInvariant();
        setCookie.Should().Contain($"{RefreshTokenCookie.Name}=").And.Contain("path=/api/v1/auth")
            .And.Contain("httponly").And.Contain("secure").And.Contain("samesite=strict").And.Contain("expires=");
    }

    private static ForwardedHeadersOptions OptionsDuRelais()
    {
        var services = new ServiceCollection().AddClientIpForwarding().BuildServiceProvider();
        return services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }

    [Fact]
    public void EtantDonneConfigurationDuRelais_QuandLaLire_AlorsSeulLEnTeteDuRelaisEstLuSurUnSaut()
    {
        var options = OptionsDuRelais();

        options.ForwardedForHeaderName.Should().Be(ClientIpForwarding.HeaderName);
        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor, "ni l'hôte ni le schéma ne sont repris du client");
        options.ForwardLimit.Should().Be(1);
        options.KnownProxies.Should().BeEmpty();
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.20.30.40", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("::1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("203.0.113.9", false)]
    [InlineData("2001:db8::1", false)]
    public void EtantDonneAdresseDuPair_QuandVerifierLaConfiance_AlorsSeulsLesReseauxPrivesSontCrus(string adresse, bool cru)
    {
        var ip = IPAddress.Parse(adresse);

        OptionsDuRelais().KnownIPNetworks.Any(n => n.Contains(ip)).Should().Be(cru);
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:198.51.100.2", "198.51.100.2")]
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::", "2001:db8:1:2::/64")]
    public void EtantDonneAdresseDuClient_QuandCalculerLaPartition_AlorsCleAttendue(string adresse, string cle)
    {
        ClientIpForwarding.PartitionKey(IPAddress.Parse(adresse)).Should().Be(cle);
    }

    [Fact]
    public void EtantDonneContexteSansAdresse_QuandCalculerLaPartition_AlorsCleCommune()
    {
        ClientIpForwarding.PartitionKey(new DefaultHttpContext()).Should().Be("inconnue");
    }
}
