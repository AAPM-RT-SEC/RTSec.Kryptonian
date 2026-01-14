using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Infrastructure.Acme;

namespace RTSec.Kryptonian.Api.Controllers;

/// <summary>
/// Controller for ACME HTTP-01 challenge validation.
/// Responds to ACME server challenge requests at /.well-known/acme-challenge/{token}.
/// </summary>
[ApiController]
[Route(".well-known/acme-challenge")]
public class AcmeChallengeController : ControllerBase
{
    private readonly Http01ChallengeStore _challengeStore;
    private readonly ILogger<AcmeChallengeController> _logger;

    public AcmeChallengeController(
        Http01ChallengeStore challengeStore,
        ILogger<AcmeChallengeController> logger)
    {
        _challengeStore = challengeStore;
        _logger = logger;
    }

    /// <summary>
    /// Responds to ACME HTTP-01 challenge validation requests.
    /// </summary>
    /// <param name="token">The challenge token from the ACME server.</param>
    [HttpGet("{token}")]
    [Produces("text/plain")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetChallenge(string token)
    {
        _logger.LogDebug("ACME challenge request received for token: {Token}", token);

        var keyAuthorization = _challengeStore.GetKeyAuthorization(token);

        if (keyAuthorization == null)
        {
            _logger.LogWarning("ACME challenge token not found: {Token}", token);
            return NotFound();
        }

        _logger.LogInformation("ACME challenge response sent for token: {Token}", token);
        return Content(keyAuthorization, "text/plain");
    }
}
