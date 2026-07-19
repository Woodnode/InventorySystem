using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using InventorySystem.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Implémentation JWT de <see cref="ITokenService"/>. Seule cette classe (Infrastructure)
/// connaît la clé de signature et le format du jeton (DIP, voir plan §3.3/§6).
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key manquante ou trop courte (32 caractères minimum). " +
                "Définis-la via `dotnet user-secrets set \"Jwt:Key\" \"<valeur>\"` " +
                "dans src/Backend/InventorySystem.Api — voir README.");
        }
    }

    public GeneratedAccessToken GenerateAccessToken(Guid userId, string email, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new GeneratedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    public DateTime GetRefreshTokenExpiryUtc() => DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
}
