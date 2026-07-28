using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetWatch.Application.Dashboard;

namespace NetWatch.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
[Produces("application/json")]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <summary>
    /// Everything the landing page renders: state counts, the probe grid, fleet uptime and
    /// the latest incidents.
    /// </summary>
    /// <param name="windowHours">Window used for the uptime figure. Defaults to 24 hours.</param>
    [HttpGet("summary")]
    [ProducesResponseType<DashboardSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryResponse>> Summary(
        [FromQuery] int windowHours = 24,
        CancellationToken cancellationToken = default) =>
        Ok(await dashboard.GetSummaryAsync(windowHours, cancellationToken));
}
