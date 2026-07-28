using System.Security.Cryptography;
using System.Text;

namespace NetWatch.Infrastructure.Identity;

/// <summary>
/// A long-lived credential used to mint new short-lived access tokens.
///
/// Only the SHA-256 hash of the token is stored. A refresh token is a bearer
/// credential just like a password, so a leaked database dump must not hand an
/// attacker working sessions. The plaintext exists only in the response to the
/// client that requested it.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    /// <summary>SHA-256 hash of the issued token, hex encoded.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// Hash of the token that superseded this one. Lets a replayed old token be traced
    /// to the chain it came from, which is how token theft is detected.
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && !IsExpired(nowUtc);

    public void Revoke(DateTime nowUtc, string? replacedByTokenHash = null)
    {
        RevokedAtUtc = nowUtc;
        ReplacedByTokenHash = replacedByTokenHash;
    }

    /// <summary>
    /// Hashes a refresh token for storage and lookup. Deterministic (no salt) because
    /// the value is already 256 bits of cryptographic randomness — it is not guessable,
    /// and the hash must be searchable to validate a presented token in one query.
    /// </summary>
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
