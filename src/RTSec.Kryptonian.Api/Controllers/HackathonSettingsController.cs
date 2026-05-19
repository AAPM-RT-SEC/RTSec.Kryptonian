using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

[ApiController]
[Route("api/settings/hackathon")]
[Produces("application/json")]
[Authorize]
public class HackathonSettingsController : ControllerBase
{
    private readonly IHackathonSettingsService _settingsService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HackathonSettingsController> _logger;

    public HackathonSettingsController(
        IHackathonSettingsService settingsService,
        IHttpClientFactory httpClientFactory,
        ILogger<HackathonSettingsController> logger)
    {
        _settingsService = settingsService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HackathonSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        return Ok(await _settingsService.GetAsync(ct));
    }

    [HttpPut]
    [ProducesResponseType(typeof(HackathonSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] HackathonSettingsUpdateDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _settingsService.UpdateAsync(dto, ct));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid hackathon settings update request");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("scoreboard")]
    [ProducesResponseType(typeof(HarnessScoreboardSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Scoreboard(CancellationToken ct)
    {
        var settings = await _settingsService.GetAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.HarnessBaseUrl))
        {
            return BadRequest(new { error = "Harness base URL is not configured." });
        }

        var url = $"{settings.HarnessBaseUrl.TrimEnd('/')}/api/scoreboard";
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new { error = "Harness scoreboard request failed.", body = text });
        }

        using var document = JsonDocument.Parse(text);
        return Ok(new HarnessScoreboardSnapshotDto
        {
            FetchedAt = DateTime.UtcNow,
            Url = url,
            Body = document.RootElement.Clone()
        });
    }
}
