using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

/// <summary>
/// Controller for EST profile management.
/// Matches OpenAPI.ESTProfiles.yaml /api/est-profiles endpoints.
/// </summary>
[ApiController]
[Route("api/est-profiles")]
[Produces("application/json")]
[Authorize]
public class EstProfilesController : ControllerBase
{
    private readonly IEstProfileService _estProfileService;
    private readonly ILogger<EstProfilesController> _logger;

    public EstProfilesController(IEstProfileService estProfileService, ILogger<EstProfilesController> logger)
    {
        _estProfileService = estProfileService;
        _logger = logger;
    }

    /// <summary>
    /// List EST profiles.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EstProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var profiles = await _estProfileService.GetAllAsync(ct);
        return Ok(profiles);
    }

    /// <summary>
    /// Create a new EST profile.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(EstProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] EstProfileCreateDto dto, CancellationToken ct)
    {
        try
        {
            var created = await _estProfileService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid EST profile create request");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot create EST profile");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get EST profile.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EstProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        var profile = await _estProfileService.GetByIdAsync(guid, ct);
        if (profile == null)
            return NotFound();

        return Ok(profile);
    }

    /// <summary>
    /// Update EST profile.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(EstProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] EstProfileUpdateDto dto, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        try
        {
            var updated = await _estProfileService.UpdateAsync(guid, dto, ct);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid EST profile update request");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot update EST profile");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete EST profile.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        var deleted = await _estProfileService.DeleteAsync(guid, ct);
        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
