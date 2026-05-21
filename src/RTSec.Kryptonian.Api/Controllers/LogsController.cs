using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Api.Logging;
using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Api.Controllers;

/// <summary>
/// Exposes the gateway's in-memory live-log ring buffer for the admin Events
/// page. Pollable endpoint: client passes the last seq it has seen and gets
/// back any newer entries plus the current head seq.
/// </summary>
[ApiController]
[Route("api/status/logs")]
[Produces("application/json")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly LiveLogBroadcaster _broadcaster;

    public LogsController(LiveLogBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    [HttpGet("recent")]
    [ProducesResponseType(typeof(LiveLogPageDto), StatusCodes.Status200OK)]
    public ActionResult<LiveLogPageDto> GetRecent(
        [FromQuery] long sinceSeq = 0,
        [FromQuery] int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 1000);
        sinceSeq = Math.Max(0, sinceSeq);
        return Ok(_broadcaster.GetSince(sinceSeq, limit));
    }
}
