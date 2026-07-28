using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Authentication;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Common.Models;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Infrastructure.Probing;

/// <summary>
/// HTTP(S) check against a path, asserting an expected status code.
/// The only probe type that says anything about whether the application actually works,
/// as opposed to whether the box is powered on.
/// </summary>
public class HttpProbeExecutor(IHttpClientFactory httpClientFactory) : IProbeExecutor
{
    /// <summary>Name of the configured client; see <c>DependencyInjection</c>.</summary>
    public const string HttpClientName = "probe";

    public ProbeType Type => ProbeType.Http;

    public async Task<ProbeExecutionResult> ExecuteAsync(Probe probe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var url = probe.BuildHttpUrl(probe.Device.Hostname);
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var timeoutSource = new CancellationTokenSource(probe.TimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Headers only: the check cares about reachability and status, and streaming
            // a large body would inflate the measured latency with transfer time.
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            stopwatch.Stop();

            var elapsed = stopwatch.Elapsed.TotalMilliseconds;
            var status = (int)response.StatusCode;

            return status == probe.ExpectedStatusCode
                ? ProbeExecutionResult.Success(elapsed, status)
                : ProbeExecutionResult.Failure(
                    ProbeOutcome.UnexpectedStatusCode,
                    $"Expected HTTP {probe.ExpectedStatusCode}, got {status} ({response.ReasonPhrase}).",
                    elapsed,
                    status);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return ProbeExecutionResult.Failure(
                ProbeOutcome.Timeout,
                $"No HTTP response from {url} within {probe.TimeoutMs}ms.",
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            return ProbeExecutionResult.Failure(Classify(ex), Describe(ex, url), stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// HttpRequestException wraps the real cause, so the inner exception decides the
    /// outcome. An expired certificate and a dead host are both "the request failed",
    /// but only one of them is fixed by restarting the server.
    /// </summary>
    private static ProbeOutcome Classify(HttpRequestException ex) => ex.InnerException switch
    {
        AuthenticationException => ProbeOutcome.TlsFailure,
        SocketException { SocketErrorCode: SocketError.ConnectionRefused } => ProbeOutcome.ConnectionRefused,
        SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData } => ProbeOutcome.DnsFailure,
        SocketException { SocketErrorCode: SocketError.HostUnreachable or SocketError.NetworkUnreachable } => ProbeOutcome.Unreachable,
        SocketException { SocketErrorCode: SocketError.TimedOut } => ProbeOutcome.Timeout,
        _ => ProbeOutcome.Error
    };

    private static string Describe(HttpRequestException ex, string url) => ex.InnerException switch
    {
        AuthenticationException tls => $"TLS handshake with {url} failed: {tls.Message}",
        SocketException { SocketErrorCode: SocketError.ConnectionRefused } => $"Connection refused by {url}.",
        SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData } => $"Could not resolve the host in {url}.",
        SocketException socket => $"Request to {url} failed: {socket.SocketErrorCode}.",
        _ => $"Request to {url} failed: {ex.Message}"
    };
}
