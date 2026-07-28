using System.ComponentModel.DataAnnotations;

namespace NetWatch.Infrastructure.Identity;

/// <summary>
/// Token settings, bound from the <c>Jwt</c> configuration section and validated at
/// startup so a missing signing key fails the boot instead of the first login.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "NetWatch";

    [Required]
    public string Audience { get; set; } = "NetWatch.Client";

    /// <summary>
    /// HMAC signing key. Must be at least 32 bytes for HS256 — a shorter key is rejected
    /// by the token handler, and a guessable one lets anyone mint an admin token.
    /// Supplied via user-secrets or an environment variable, never committed.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Short by design. Access tokens cannot be revoked once issued, so the window in
    /// which a stolen one is useful is bounded by this value alone.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// Long-lived but revocable: refresh tokens are stored (hashed) and rotated on use,
    /// so a leaked one can be killed server-side.
    /// </summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 7;
}
