using NetWatch.Application.Auth;

namespace NetWatch.Application.Common.Interfaces;

/// <summary>
/// Authentication operations. Implemented in Infrastructure, where ASP.NET Core Identity
/// and the JWT libraries live, so controllers depend only on this contract.
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a valid refresh token for a new pair, rotating the old one.
    /// </summary>
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Revokes a refresh token, ending that session. Idempotent.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Raised when credentials or a refresh token are rejected. Deliberately carries no detail
/// about which part failed — telling a caller that an email exists but the password is
/// wrong is a free account-enumeration oracle.
/// </summary>
public class AuthenticationFailedException(string message) : Exception(message);
