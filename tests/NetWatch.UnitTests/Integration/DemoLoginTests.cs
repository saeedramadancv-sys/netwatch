using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NetWatch.Application.Auth;

namespace NetWatch.UnitTests.Integration;

public class DemoEnabledApiFactory : NetWatchApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Enabled"] = "true" }));
    }
}

public class DemoLoginTests(DemoEnabledApiFactory factory) : IClassFixture<DemoEnabledApiFactory>
{
    [Fact]
    public async Task TheDemoSessionHasOnlyTheViewerRole()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/demo", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.User.Roles.Should().Equal("Viewer");
    }

    [Fact]
    public async Task TheDemoSessionCanReadButNotChangeAnything()
    {
        // This is the promise the public button makes, so it is tested against the real
        // authorization pipeline rather than assumed from the role name.
        using var client = factory.CreateClient();
        var login = await client.PostAsync("/api/auth/demo", null);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var read = await client.GetAsync("/api/devices");
        var write = await client.PostAsJsonAsync("/api/devices", new { name = "x", address = "1.1.1.1" });
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@b.c", password = "Password#1", fullName = "x" });

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        register.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

public class DemoDisabledTests(NetWatchApiFactory factory) : IClassFixture<NetWatchApiFactory>
{
    [Fact]
    public async Task WithoutTheSettingTheDemoEndpointDoesNotExist()
    {
        using var client = factory.CreateClient();

        var login = await client.PostAsync("/api/auth/demo", null);
        var status = await client.GetFromJsonAsync<DemoStatusBody>("/api/auth/demo");

        login.StatusCode.Should().Be(HttpStatusCode.NotFound);
        status!.Enabled.Should().BeFalse();
    }

    private sealed record DemoStatusBody(bool Enabled);
}
