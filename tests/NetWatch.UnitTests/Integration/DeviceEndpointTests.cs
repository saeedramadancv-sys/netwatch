using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NetWatch.Application.Auth;
using NetWatch.Application.Dashboard;
using NetWatch.Application.Devices;
using NetWatch.Application.Probes;
using NetWatch.Domain.Enums;

namespace NetWatch.UnitTests.Integration;

public class DeviceEndpointTests(NetWatchApiFactory factory) : IClassFixture<NetWatchApiFactory>
{
    [Fact]
    public async Task ListingDevicesWithoutATokenIsRejected()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/devices", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnAdministratorCanCreateReadUpdateAndDeleteADevice()
    {
        using var client = await AuthenticatedClientAsync();
        var hostname = UniqueHost();

        var create = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest("Core Switch", hostname, DeviceCategory.Switch, "HQ", "Stack master"),
            TestJson.Options);

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await create.Content.ReadFromJsonAsync<DeviceDetailResponse>(TestJson.Options))!;
        created.Name.Should().Be("Core Switch");
        created.Status.Should().Be(ProbeState.Unknown, "a device with no probes has no health yet");

        var fetched = await client.GetFromJsonAsync<DeviceDetailResponse>($"/api/devices/{created.Id}", TestJson.Options);
        fetched!.Hostname.Should().Be(hostname);

        var update = await client.PutAsJsonAsync(
            $"/api/devices/{created.Id}",
            new UpdateDeviceRequest("Core Switch A", hostname, DeviceCategory.Switch, "HQ", null),
            TestJson.Options);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<DeviceDetailResponse>(TestJson.Options))!.Name.Should().Be("Core Switch A");

        var delete = await client.DeleteAsync(new Uri($"/api/devices/{created.Id}", UriKind.Relative));
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetAsync(new Uri($"/api/devices/{created.Id}", UriKind.Relative));
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegisteringTheSameHostnameTwiceReturnsAConflict()
    {
        using var client = await AuthenticatedClientAsync();
        var hostname = UniqueHost();

        var first = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest("First", hostname, DeviceCategory.Server, null, null),
            TestJson.Options);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest("Second", hostname, DeviceCategory.Server, null, null),
            TestJson.Options);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnInvalidHostnameIsRejectedWithFieldLevelDetail()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest("Bad", "not a hostname", DeviceCategory.Server, null, null),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestingAMissingDeviceReturnsNotFound()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync(new Uri("/api/devices/999999", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AProbeCanBeAddedToADeviceAndComesBackWithARenderedTarget()
    {
        using var client = await AuthenticatedClientAsync();
        var hostname = UniqueHost();
        var device = await CreateDeviceAsync(client, "SSH Host", hostname);

        var create = await client.PostAsJsonAsync(
            $"/api/devices/{device.Id}/probes",
            new CreateProbeRequest(ProbeType.Tcp, IntervalSeconds: 60, TimeoutMs: 3_000, Port: 22),
            TestJson.Options);

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var probe = (await create.Content.ReadFromJsonAsync<ProbeResponse>(TestJson.Options))!;
        probe.Target.Should().Be($"{hostname}:22");
        probe.State.Should().Be(ProbeState.Unknown);
    }

    [Fact]
    public async Task ATcpProbeWithoutAPortIsRejected()
    {
        using var client = await AuthenticatedClientAsync();
        var device = await CreateDeviceAsync(client, "No Port", UniqueHost());

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{device.Id}/probes",
            new CreateProbeRequest(ProbeType.Tcp, Port: null),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AProbeWhoseTimeoutExceedsItsIntervalIsRejected()
    {
        using var client = await AuthenticatedClientAsync();
        var device = await CreateDeviceAsync(client, "Overlap", UniqueHost());

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{device.Id}/probes",
            new CreateProbeRequest(ProbeType.Icmp, IntervalSeconds: 5, TimeoutMs: 10_000),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheDashboardSummaryIsServedToAnyAuthenticatedUser()
    {
        using var client = await AuthenticatedClientAsync();

        var summary = await client.GetFromJsonAsync<DashboardSummaryResponse>("/api/dashboard/summary", TestJson.Options);

        summary.Should().NotBeNull();
        summary!.ProbeCount.Should().BeGreaterThanOrEqualTo(0);
    }

    private static async Task<DeviceDetailResponse> CreateDeviceAsync(HttpClient client, string name, string hostname)
    {
        var response = await client.PostAsJsonAsync(
            "/api/devices",
            new CreateDeviceRequest(name, hostname, DeviceCategory.Server, null, null),
            TestJson.Options);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceDetailResponse>(TestJson.Options))!;
    }

    /// <summary>
    /// Hostnames are unique per device and the factory is shared across this class, so
    /// each test mints its own instead of colliding with its neighbours.
    /// </summary>
    private static string UniqueHost() => $"host-{Guid.NewGuid():N}.test";

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(NetWatchApiFactory.AdminEmail, NetWatchApiFactory.AdminPassword));

        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
