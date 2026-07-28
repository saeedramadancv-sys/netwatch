namespace NetWatch.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string Email, string Password, string FullName, string Role);

/// <summary>
/// Refresh takes only the refresh token: the expired access token adds nothing, since
/// the refresh token is what proves the session is still valid.
/// </summary>
public sealed record RefreshRequest(string RefreshToken);

public sealed record RevokeRequest(string RefreshToken);

/// <summary>
/// Issued credentials. The refresh token is returned in the body rather than a cookie
/// because the client is a separate-origin SPA; the trade-off is documented in the README.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserInfo User);

public sealed record UserInfo(
    string Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);
