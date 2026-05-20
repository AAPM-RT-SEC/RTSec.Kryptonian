using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

[ApiController]
[Route("api/settings/gateway")]
[Produces("application/json")]
[Authorize]
public class GatewaySettingsController : ControllerBase
{
    private readonly IGatewaySettingsService _settingsService;
    private readonly ILogger<GatewaySettingsController> _logger;

    public GatewaySettingsController(
        IGatewaySettingsService settingsService,
        ILogger<GatewaySettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GatewaySettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        return Ok(await _settingsService.GetAsync(ct));
    }

    [HttpPut]
    [ProducesResponseType(typeof(GatewaySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] GatewaySettingsUpdateDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _settingsService.UpdateAsync(dto, ct));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid gateway settings update request");
            return BadRequest(new { error = ex.Message });
        }
    }
}
