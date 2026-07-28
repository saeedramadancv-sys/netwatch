using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetWatch.Application.Incidents;
using NetWatch.Domain.Enums;
using NetWatch.Infrastructure.Identity;

namespace NetWatch.Api.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize]
[Produces("application/json")]
public class IncidentsController(IIncidentService incidents) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IncidentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IncidentResponse>>> List(
        [FromQuery] IncidentStatus? status,
        [FromQuery] int? deviceId,
        [FromQuery] DateTime? from,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await incidents.ListAsync(new IncidentQuery(status, deviceId, from, take), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentResponse>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await incidents.GetAsync(id, cancellationToken));

    /// <summary>
    /// Claims an open incident. Records who is handling it so two operators do not chase
    /// the same outage; the incident still resolves automatically when the probe recovers.
    /// </summary>
    [HttpPost("{id:int}/acknowledge")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentResponse>> Acknowledge(int id, CancellationToken cancellationToken)
    {
        // Taken from the token, never from the request body: a client must not be able to
        // acknowledge an incident on someone else's behalf.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "unknown";

        return Ok(await incidents.AcknowledgeAsync(id, userId, cancellationToken));
    }
}
