using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Produces("application/json")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IEstProfileService _estProfileService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IDeviceService deviceService,
        IEstProfileService estProfileService,
        ILogger<DevicesController> logger)
    {
        _deviceService = deviceService;
        _estProfileService = estProfileService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var devices = await _deviceService.GetAllAsync(ct);
        return Ok(devices);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] DeviceCreateDto dto, CancellationToken ct)
    {
        try
        {
            var created = await _deviceService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid device create request");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("/api/device-requests")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DeviceApprovalRequestResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestApproval([FromBody] DeviceApprovalRequestDto dto, CancellationToken ct)
    {
        try
        {
            var response = await _deviceService.RequestApprovalAsync(dto, ct);
            return Accepted(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid device approval request");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        var device = await _deviceService.GetByIdAsync(guid, ct);
        return device == null ? NotFound() : Ok(device);
    }

    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        var device = await _deviceService.ApproveAsync(guid, ct);
        return device == null ? NotFound() : Ok(device);
    }

    [HttpPost("{id}/remove")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        var device = await _deviceService.RemoveAsync(guid, ct);
        return device == null ? NotFound() : Ok(device);
    }

    [HttpGet("{id}/certificates")]
    [ProducesResponseType(typeof(IEnumerable<CertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Certificates(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        if (await _deviceService.GetByIdAsync(guid, ct) == null)
            return NotFound();

        var certificates = await _deviceService.GetCertificatesAsync(guid, ct);
        return Ok(certificates);
    }

    [HttpPost("{id}/demo-enroll")]
    [ProducesResponseType(typeof(DemoEnrollResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DemoEnroll(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return NotFound();

        try
        {
            var profileId = await ResolveDefaultProfileIdAsync(ct);
            if (profileId == null)
            {
                return BadRequest(new { error = "No enabled EST profile is configured." });
            }

            var response = await _deviceService.DemoEnrollAsync(guid, profileId.Value, ct);
            return response == null ? NotFound() : Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Demo enrollment failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<Guid?> ResolveDefaultProfileIdAsync(CancellationToken ct)
    {
        var profiles = await _estProfileService.GetAllAsync(ct);
        var profile = profiles.FirstOrDefault(p => p.IsEnabled);
        return profile == null || !Guid.TryParse(profile.Id, out var profileId) ? null : profileId;
    }
}
