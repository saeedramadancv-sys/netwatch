using System.Diagnostics;
using System.Net.Sockets;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Common.Models;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Infrastructure.Probing;

/// <summary>
/// TCP connect check. Stronger evidence than ICMP: it proves something is actually
/// listening on the port, not merely that the host answers pings.
/// </summary>
public class TcpProbeExecutor : IProbeExecutor
{
    public ProbeType Type => ProbeType.Tcp;

    public async Task<ProbeExecutionResult> ExecuteAsync(Probe probe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var host = probe.Device.Hostname;
        var port = probe.Port!.Value;

        // Linked so the probe's own timeout fires independently of application shutdown,
        // and the two can be told apart afterwards.
        using var timeoutSource = new CancellationTokenSource(probe.TimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        using var client = new TcpClient();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await client.ConnectAsync(host, port, linked.Token);
            stopwatch.Stop();
            return ProbeExecutionResult.Success(stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return ProbeExecutionResult.Failure(
                ProbeOutcome.Timeout,
                $"TCP connect to {host}:{port} timed out after {probe.TimeoutMs}ms.",
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException ex)
        {
            return ProbeExecutionResult.Failure(Classify(ex.SocketErrorCode), FormatSocketError(ex, host, port));
        }
    }

    /// <summary>
    /// Maps socket errors onto outcomes. The distinction matters operationally:
    /// a refused connection means the host is alive and the service is dead, while
    /// a timeout usually means a firewall silently dropped the packet.
    /// </summary>
    private static ProbeOutcome Classify(SocketError error) => error switch
    {
        SocketError.ConnectionRefused => ProbeOutcome.ConnectionRefused,
        SocketError.HostNotFound or SocketError.NoData => ProbeOutcome.DnsFailure,
        SocketError.TimedOut => ProbeOutcome.Timeout,
        SocketError.HostUnreachable or SocketError.NetworkUnreachable or SocketError.NetworkDown => ProbeOutcome.Unreachable,
        _ => ProbeOutcome.Error
    };

    private static string FormatSocketError(SocketException ex, string host, int port) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused => $"Connection refused by {host}:{port} — nothing is listening.",
        SocketError.HostNotFound or SocketError.NoData => $"Could not resolve '{host}'.",
        SocketError.HostUnreachable or SocketError.NetworkUnreachable => $"No route to {host}.",
        _ => $"TCP connect to {host}:{port} failed: {ex.SocketErrorCode}."
    };
}
