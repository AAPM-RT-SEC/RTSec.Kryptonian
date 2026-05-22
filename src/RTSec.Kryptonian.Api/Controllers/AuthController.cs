using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;

namespace RTSec.Kryptonian.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(JwtClaimNames.Name);
        var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtClaimNames.Sub);

        var status = await _authService.GetStatusAsync(isAuthenticated, username, role, userId, ct);
        return Ok(status);
    }

    [HttpPost("setup")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Setup([FromBody] SetupRequestDto dto, CancellationToken ct)
    {
        try
        {
            var response = await _authService.BootstrapAsync(dto, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
    {
        try
        {
            var response = await _authService.LoginAsync(dto, ct);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            // Don't leak details about why auth failed
            return Unauthorized(new { error = "Invalid username or password." });
        }
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        // JWT is stateless — client clears the token
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtClaimNames.Sub);
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(JwtClaimNames.Name);
        var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        return Ok(new { id = userId, username, role });
    }

    private static class JwtClaimNames
    {
        public const string Sub = "sub";
        public const string Name = "name";
    }
}
