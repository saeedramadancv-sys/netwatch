using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetWatch.Application.Probes;
using NetWatch.Infrastructure.Identity;

namespace NetWatch.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[Produces("application/json")]
public class ProbesController(IProbeService probes, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Maximum points a history request will return, regardless of the range asked for.</summary>
    private const int MaxHistoryPoints = 1_000;

    [HttpGet("probes/{id:int}")]
    [ProducesResponseType<ProbeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProbeResponse>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await probes.GetAsync(id, cancellationToken));

    [HttpPost("devices/{deviceId:int}/probes")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType<ProbeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProbeResponse>> Create(int deviceId, CreateProbeRequest request, CancellationToken cancellationToken)
    {
        var created = await probes.CreateAsync(deviceId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("probes/{id:int}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType<ProbeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProbeResponse>> Update(int id, UpdateProbeRequest request, CancellationToken cancellationToken) =>
        Ok(await probes.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("probes/{id:int}/enabled")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetEnabled(int id, [FromQuery] bool enabled, CancellationToken cancellationToken)
    {
        await probes.SetEnabledAsync(id, enabled, cancellationToken);
        return NoContent();
    }

    [HttpDelete("probes/{id:int}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await probes.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Measurement history and aggregates for a probe.
    /// </summary>
    /// <param name="from">Start of the window. Defaults to 24 hours ago.</param>
    /// <param name="to">End of the window. Defaults to now.</param>
    /// <param name="maxPoints">
    /// Cap on returned samples, so a request for a month of 10-second checks returns a
    /// chartable series instead of a quarter of a million rows.
    /// </param>
    [HttpGet("probes/{id:int}/history")]
    [ProducesResponseType<ProbeHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProbeHistoryResponse>> History(
        int id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int maxPoints = 500,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var toUtc = to ?? now;
        var fromUtc = from ?? toUtc.AddHours(-24);

        if (fromUtc >= toUtc)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid range",
                Detail = "'from' must be earlier than 'to'."
            });
        }

        var history = await probes.GetHistoryAsync(
            id,
            fromUtc,
            toUtc,
            Math.Clamp(maxPoints, 1, MaxHistoryPoints),
            cancellationToken);

        return Ok(history);
    }

    /// <summary>
    /// Runs the check immediately. Useful after fixing a device, when waiting a full
    /// interval to see green again is the difference between a demo and a shrug.
    /// </summary>
    [HttpPost("probes/{id:int}/run")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType<ProbeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProbeResponse>> RunNow(int id, CancellationToken cancellationToken) =>
        Ok(await probes.RunNowAsync(id, cancellationToken));
}
