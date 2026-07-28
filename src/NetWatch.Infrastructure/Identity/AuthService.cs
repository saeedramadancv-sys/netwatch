using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetWatch.Application.Auth;
using NetWatch.Application.Common.Exceptions;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Infrastructure.Persistence;

namespace NetWatch.Infrastructure.Identity;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    AppDbContext context,
    TokenService tokens,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Same message and roughly the same work as a wrong password, so response
            // content and timing do not reveal whether the account exists.
            logger.LogWarning("Login attempt for unknown email from {Ip}.", ipAddress);
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        // CheckPasswordSignInAsync (not CheckPasswordAsync) so Identity's lockout counter
        // is honoured; otherwise the configured 5-attempt lockout never engages.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Locked-out account {UserId} attempted to sign in from {Ip}.", user.Id, ipAddress);
            throw new AuthenticationFailedException("This account is temporarily locked. Try again later.");
        }

        if (!result.Succeeded)
        {
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        user.LastLoginAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await userManager.UpdateAsync(user);

        return await IssueAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = string.IsNullOrWhiteSpace(request.Role) ? AppRoles.Viewer : request.Role;

        if (!await roleManager.RoleExistsAsync(role))
        {
            throw new ConflictException($"Role '{role}' does not exist.");
        }

        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new DomainValidationException(created.Errors.Select(e => e.Description));
        }

        await userManager.AddToRoleAsync(user, role);
        logger.LogInformation("Registered user {UserId} with role {Role}.", user.Id, role);

        return await IssueAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = RefreshToken.Hash(refreshToken ?? string.Empty);

        var stored = await context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        if (!stored.IsActive(now))
        {
            // A revoked token being presented means either a stale client or a stolen
            // token being replayed. Killing the whole chain is the safe response: the
            // legitimate user re-authenticates, the attacker gets nothing.
            logger.LogWarning("Reuse of an inactive refresh token for user {UserId} from {Ip}.", stored.UserId, ipAddress);
            await RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        var user = stored.User ?? throw new AuthenticationFailedException("Invalid refresh token.");

        // Rotation: the presented token is retired as part of issuing its replacement, so
        // each refresh token is usable exactly once.
        var issued = await IssueAsync(user, ipAddress, cancellationToken, replacing: stored);
        return issued;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = RefreshToken.Hash(refreshToken ?? string.Empty);
        var stored = await context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        // Idempotent: revoking an unknown or already-revoked token is a no-op, and
        // reporting "not found" would confirm which tokens exist.
        if (stored is null || stored.RevokedAtUtc is not null)
        {
            return;
        }

        stored.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken,
        RefreshToken? replacing = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpires) = tokens.CreateAccessToken(user, roles);
        var (refreshToken, refreshRecord) = tokens.CreateRefreshToken(user.Id, ipAddress);

        replacing?.Revoke(timeProvider.GetUtcNow().UtcDateTime, refreshRecord.TokenHash);

        context.RefreshTokens.Add(refreshRecord);
        await context.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            accessExpires,
            refreshToken,
            refreshRecord.ExpiresAtUtc,
            new UserInfo(user.Id, user.Email ?? string.Empty, user.FullName, [.. roles]));
    }

    private async Task RevokeAllForUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, nowUtc), cancellationToken);
    }
}
