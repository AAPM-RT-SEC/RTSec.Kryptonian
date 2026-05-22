using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

[ApiController]
[Route("api/users/{userId}/api-keys")]
[Produces("application/json")]
[Authorize]
public class ApiKeysController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(IUserService userService, ILogger<ApiKeysController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApiKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var userGuid)) return NotFound();

        var requestingUserId = GetRequestingUserId();
        var isSystemAdmin = IsSystemAdmin();

        if (!isSystemAdmin && requestingUserId != userGuid)
            return Forbid();

        var keys = await _userService.GetApiKeysAsync(userGuid, ct);
        return Ok(keys);
    }

    [HttpPost]
    [Authorize(Policy = "DeviceAdmin")]
    [ProducesResponseType(typeof(GenerateApiKeyResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Generate(string userId, [FromBody] GenerateApiKeyRequestDto dto, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var userGuid)) return NotFound();

        var requestingUserId = GetRequestingUserId();
        var isSystemAdmin = IsSystemAdmin();

        if (!isSystemAdmin && requestingUserId != userGuid)
            return Forbid();

        try
        {
            var result = await _userService.GenerateApiKeyAsync(userGuid, dto, ct);
            if (result == null) return NotFound();
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{keyId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(string userId, string keyId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var userGuid)) return NotFound();
        if (!Guid.TryParse(keyId, out var keyGuid)) return NotFound();

        var requestingUserId = GetRequestingUserId();
        var isSystemAdmin = IsSystemAdmin();

        try
        {
            var revoked = await _userService.RevokeApiKeyAsync(keyGuid, requestingUserId, isSystemAdmin, ct);
            return revoked ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private Guid GetRequestingUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private bool IsSystemAdmin()
    {
        var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        return role == "SystemAdmin";
    }
}
