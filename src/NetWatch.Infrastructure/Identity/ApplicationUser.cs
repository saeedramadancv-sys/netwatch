using Microsoft.AspNetCore.Identity;

namespace NetWatch.Infrastructure.Identity;

/// <summary>
/// The application's user, extending ASP.NET Core Identity.
///
/// Identity types live in Infrastructure rather than Domain on purpose: they are tied
/// to a specific authentication framework, and the domain has no opinion about how a
/// human proves who they are. Domain entities reference users by id string only.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
