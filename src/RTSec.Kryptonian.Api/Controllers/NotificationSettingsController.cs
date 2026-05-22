using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

[ApiController]
[Route("api/settings/notifications")]
[Produces("application/json")]
[Authorize]
public class NotificationSettingsController : ControllerBase
{
    private readonly INotificationSettingsService _service;
    private readonly ILogger<NotificationSettingsController> _logger;

    public NotificationSettingsController(
        INotificationSettingsService service,
        ILogger<NotificationSettingsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(NotificationSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        return Ok(await _service.GetAsync(ct));
    }

    [HttpPut]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NotificationSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] NotificationSettingsUpdateDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.UpdateAsync(dto, ct));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid notification settings update request");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("recipients")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationRecipientDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRecipients(CancellationToken ct)
    {
        return Ok(await _service.ListRecipientsAsync(ct));
    }

    [HttpPost("recipients")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(NotificationRecipientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertRecipient(
        [FromBody] NotificationRecipientUpsertDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.UpsertRecipientAsync(dto, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("recipients/{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRecipient(Guid id, CancellationToken ct)
    {
        return await _service.DeleteRecipientAsync(id, ct) ? NoContent() : NotFound();
    }

    [HttpPost("test")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Test([FromBody] NotificationTestRequestDto request, CancellationToken ct)
    {
        try
        {
            await _service.SendTestEmailAsync(request, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test email failed");
            return BadRequest(new { error = $"SMTP test failed: {ex.Message}" });
        }
    }
}
