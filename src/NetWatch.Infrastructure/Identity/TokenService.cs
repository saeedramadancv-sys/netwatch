using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace NetWatch.Infrastructure.Identity;

/// <summary>
/// Mints access and refresh tokens.
/// </summary>
public class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),

            // A unique token id lets a specific token be traced in logs without logging
            // the token itself.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = expires,
            IssuedAt = now,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expires);
    }

    /// <summary>
    /// Creates a refresh token and the record that tracks it.
    ///
    /// The plaintext is returned to the caller once and never stored: only its hash is
    /// persisted, so a database leak cannot be replayed as a live session.
    /// </summary>
    public (string Token, RefreshToken Record) CreateRefreshToken(string userId, string? ipAddress)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 256 bits from a cryptographic RNG. Guid would be smaller and partly structured;
        // this is unguessable by construction.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var record = new RefreshToken
        {
            UserId = userId,
            TokenHash = RefreshToken.Hash(token),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ipAddress
        };

        return (token, record);
    }
}
