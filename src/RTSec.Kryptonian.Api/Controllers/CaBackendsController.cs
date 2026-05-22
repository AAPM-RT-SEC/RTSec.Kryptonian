using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

/// <summary>
/// Controller for CA backend management.
/// Matches OpenAPI.Admin.yaml /api/cas endpoints.
/// </summary>
[ApiController]
[Route("api/cas")]
[Produces("application/json")]
[Authorize]
public class CaBackendsController : ControllerBase
{
    private readonly ICaBackendService _caBackendService;
    private readonly ILogger<CaBackendsController> _logger;

    public CaBackendsController(ICaBackendService caBackendService, ILogger<CaBackendsController> logger)
    {
        _caBackendService = caBackendService;
        _logger = logger;
    }

    /// <summary>
    /// List configured CA backends.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CaBackendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var backends = await _caBackendService.GetAllAsync(ct);
        return Ok(backends);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(CaBackendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var backend = await _caBackendService.GetActiveAsync(ct);
        return backend == null ? NotFound(new { error = "No active CA backend configured" }) : Ok(backend);
    }

    /// <summary>
    /// Create a new CA backend configuration.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CaBackendDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CaBackendCreateDto dto, CancellationToken ct)
    {
        try
        {
            var created = await _caBackendService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid CA backend create request");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get CA backend config.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CaBackendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        var backend = await _caBackendService.GetByIdAsync(guid, ct);
        if (backend == null)
            return NotFound();

        return Ok(backend);
    }

    /// <summary>
    /// Update CA backend config.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CaBackendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] CaBackendUpdateDto dto, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        try
        {
            var updated = await _caBackendService.UpdateAsync(guid, dto, ct);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid CA backend update request");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete CA backend.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        try
        {
            var deleted = await _caBackendService.DeleteAsync(guid, ct);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot delete CA backend");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test connection to CA backend.
    /// </summary>
    [HttpPost("{id}/test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnection(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        var success = await _caBackendService.TestConnectionAsync(guid, ct);
        return Ok(new { success });
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CaBackendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        try
        {
            var backend = await _caBackendService.ActivateAsync(guid, ct);
            return backend == null ? NotFound() : Ok(backend);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid CA backend activation request");
            return BadRequest(new { error = ex.Message });
        }
    }
}
