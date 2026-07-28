using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NetWatch.Application.Auth;

namespace NetWatch.UnitTests.Integration;

public class AuthEndpointTests(NetWatchApiFactory factory) : IClassFixture<NetWatchApiFactory>
{
    [Fact]
    public async Task ValidCredentials_ReturnATokenPairAndTheUsersRoles()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(NetWatchApiFactory.AdminEmail, NetWatchApiFactory.AdminPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        auth.User.Roles.Should().Contain("Admin");
        auth.AccessTokenExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task AWrongPasswordIsRejectedWithoutRevealingWhichPartFailed()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(NetWatchApiFactory.AdminEmail, "definitely-not-the-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task AnUnknownEmailProducesTheSameResponseAsAWrongPassword()
    {
        // Identical responses are what stop the endpoint being an account-enumeration oracle.
        using var client = factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@netwatch.test", "whatever"));
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(NetWatchApiFactory.AdminEmail, "whatever"));

        unknown.StatusCode.Should().Be(wrongPassword.StatusCode);

        // Compared field by field rather than as raw strings: every problem response
        // carries its own traceId, which is meant to differ and says nothing about the
        // account.
        var a = await unknown.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);
        var b = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);

        a!.Title.Should().Be(b!.Title);
        a.Detail.Should().Be(b.Detail);
        a.Status.Should().Be(b.Status);
    }

    [Fact]
    public async Task AnEmptyEmailIsRejectedByValidationBeforeAnyLookup()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("", ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ARefreshTokenCanBeExchangedForANewPair()
    {
        using var client = factory.CreateClient();
        var auth = await LoginAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>();
        refreshed!.RefreshToken.Should().NotBe(auth.RefreshToken, "refresh tokens are rotated on use");
    }

    [Fact]
    public async Task AReusedRefreshTokenIsRejected()
    {
        // Rotation means a token is valid exactly once; a replay is treated as theft.
        using var client = factory.CreateClient();
        var auth = await LoginAsync(client);

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ARevokedRefreshTokenStopsWorking()
    {
        using var client = factory.CreateClient();
        var auth = await LoginAsync(client);

        var revoke = await client.PostAsJsonAsync("/api/auth/revoke", new RevokeRequest(auth.RefreshToken));
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterRevoke = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokingAnUnknownTokenSucceedsSilently()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/revoke", new RevokeRequest("not-a-real-token"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MeReturnsTheIdentityEncodedInTheToken()
    {
        using var client = factory.CreateClient();
        var auth = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var me = await client.GetFromJsonAsync<UserInfo>("/api/auth/me");

        me!.Email.Should().Be(NetWatchApiFactory.AdminEmail);
        me.Roles.Should().Contain("Admin");
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(NetWatchApiFactory.AdminEmail, NetWatchApiFactory.AdminPassword));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}
