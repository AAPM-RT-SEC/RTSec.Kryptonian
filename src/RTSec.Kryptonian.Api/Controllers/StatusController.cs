using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

/// <summary>
/// Controller for status and health endpoints.
/// Matches OpenAPI.StatusObserve.yaml /api/status/* endpoints.
/// </summary>
[ApiController]
[Route("api/status")]
[Produces("application/json")]
[Authorize]
public class StatusController : ControllerBase
{
    private readonly IEnrollmentEventService _enrollmentEventService;

    public StatusController(IEnrollmentEventService enrollmentEventService)
    {
        _enrollmentEventService = enrollmentEventService;
    }

    /// <summary>
    /// List recent enrollment events.
    /// </summary>
    [HttpGet("enrollments")]
    [ProducesResponseType(typeof(IEnumerable<EnrollmentEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnrollments(
        [FromQuery] string? profileId = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        Guid? profileGuid = null;
        if (!string.IsNullOrEmpty(profileId))
        {
            if (!Guid.TryParse(profileId, out var parsed))
                return BadRequest(new { error = "Invalid profileId format" });
            profileGuid = parsed;
        }

        // Clamp limit to reasonable bounds
        limit = Math.Clamp(limit, 1, 1000);

        var events = await _enrollmentEventService.GetRecentAsync(profileGuid, limit, ct);
        return Ok(events);
    }
}
