using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetWatch.Application.Auth;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Infrastructure.Identity;

namespace NetWatch.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Exchanges credentials for an access token and a refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.LoginAsync(request, ClientIp(), cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Creates an account. Administrator-only: this is an internal monitoring tool, so
    /// self-service registration would let anyone who can reach the API enrol themselves.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.RegisterAsync(request, ClientIp(), cancellationToken);
        return Ok(response);
    }

    /// <summary>Rotates a refresh token, returning a fresh pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.RefreshAsync(request.RefreshToken, ClientIp(), cancellationToken);
        return Ok(response);
    }

    /// <summary>Ends a session by revoking its refresh token. Idempotent.</summary>
    [HttpPost("revoke")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(RevokeRequest request, CancellationToken cancellationToken)
    {
        await auth.RevokeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>Returns the caller's identity as the server sees it, from the token claims.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    public ActionResult<UserInfo> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? string.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? string.Empty;
        var name = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new UserInfo(id, email, name, roles));
    }

    /// <summary>
    /// Best-effort client address, used only to annotate issued refresh tokens.
    /// Behind a proxy this is the proxy unless forwarded headers are configured.
    /// </summary>
    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
