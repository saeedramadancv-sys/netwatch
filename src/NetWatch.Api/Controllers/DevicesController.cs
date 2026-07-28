using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetWatch.Application.Devices;
using NetWatch.Domain.Enums;
using NetWatch.Infrastructure.Identity;

namespace NetWatch.Api.Controllers;

/// <summary>
/// Device inventory.
///
/// Reading is open to any authenticated user; changes require Operator, and deletion
/// requires Admin because it discards the device's entire measurement history.
/// </summary>
[ApiController]
[Route("api/devices")]
[Authorize]
[Produces("application/json")]
public class DevicesController(IDeviceService devices) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeviceResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] DeviceCategory? category,
        [FromQuery] string? site,
        CancellationToken cancellationToken)
    {
        var result = await devices.ListAsync(search, category, site, cancellationToken);
        return Ok(result);
    }

    /// <summary>Distinct site names, for populating the filter dropdown.</summary>
    [HttpGet("sites")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Sites(CancellationToken cancellationToken) =>
        Ok(await devices.ListSitesAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<DeviceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDetailResponse>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await devices.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType<DeviceDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceDetailResponse>> Create(CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        var created = await devices.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType<DeviceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDetailResponse>> Update(int id, UpdateDeviceRequest request, CancellationToken cancellationToken) =>
        Ok(await devices.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Parks or resumes every probe on the device. The non-destructive alternative to
    /// deletion, and the right tool for planned maintenance.
    /// </summary>
    [HttpPatch("{id:int}/enabled")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnabled(int id, [FromQuery] bool enabled, CancellationToken cancellationToken)
    {
        await devices.SetEnabledAsync(id, enabled, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await devices.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
