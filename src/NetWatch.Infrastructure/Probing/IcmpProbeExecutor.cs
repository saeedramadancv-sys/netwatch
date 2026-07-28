using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Common.Models;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Infrastructure.Probing;

/// <summary>
/// ICMP echo. The cheapest reachability check, and the least reliable signal:
/// plenty of hosts are configured to drop echo requests while serving traffic fine,
/// and many hosting platforms deny the raw sockets ICMP needs. Both cases are reported
/// distinctly rather than being flattened into "down".
/// </summary>
public class IcmpProbeExecutor(ILogger<IcmpProbeExecutor> logger) : IProbeExecutor
{
    public ProbeType Type => ProbeType.Icmp;

    public async Task<ProbeExecutionResult> ExecuteAsync(Probe probe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var host = probe.Device.Hostname;

        using var ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(host, TimeSpan.FromMilliseconds(probe.TimeoutMs), cancellationToken: cancellationToken);

            return reply.Status switch
            {
                IPStatus.Success => ProbeExecutionResult.Success(reply.RoundtripTime),

                IPStatus.TimedOut => ProbeExecutionResult.Failure(
                    ProbeOutcome.Timeout,
                    $"No ICMP reply within {probe.TimeoutMs}ms."),

                IPStatus.DestinationHostUnreachable
                    or IPStatus.DestinationNetworkUnreachable
                    or IPStatus.DestinationUnreachable
                    or IPStatus.DestinationPortUnreachable => ProbeExecutionResult.Failure(
                        ProbeOutcome.Unreachable,
                        $"ICMP unreachable: {reply.Status}."),

                _ => ProbeExecutionResult.Failure(ProbeOutcome.Error, $"ICMP failed: {reply.Status}.")
            };
        }
        catch (PingException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.HostNotFound })
        {
            return ProbeExecutionResult.Failure(ProbeOutcome.DnsFailure, $"Could not resolve '{host}'.");
        }
        catch (PingException ex)
        {
            return ProbeExecutionResult.Failure(ProbeOutcome.Error, Describe(ex));
        }
        catch (SocketException ex)
        {
            // Containers and shared hosts routinely forbid raw ICMP sockets. Reporting
            // this as an outage would be a lie about the target, so it is surfaced as a
            // probe error and logged loudly enough to point at the fix (use a TCP or
            // HTTP probe instead).
            logger.LogWarning(ex, "ICMP is unavailable in this environment for probe {ProbeId}. Consider a TCP or HTTP probe.", probe.Id);
            return ProbeExecutionResult.Failure(ProbeOutcome.Error, "ICMP is not permitted in this environment.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProbeExecutionResult.Failure(ProbeOutcome.Timeout, $"ICMP timed out after {probe.TimeoutMs}ms.");
        }
    }

    private static string Describe(Exception ex) =>
        ex.InnerException is null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
}
