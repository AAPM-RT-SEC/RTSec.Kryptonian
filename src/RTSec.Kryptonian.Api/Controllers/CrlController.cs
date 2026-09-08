using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Api.Controllers;

[ApiController]
[Route("api/crl")]
[AllowAnonymous]
public class CrlController(ICertificateRevocationService revocationService) : ControllerBase
{
    [HttpGet("{issuerFingerprint}.crl")]
    public async Task<IActionResult> Get(string issuerFingerprint, CancellationToken ct)
    {
        var crl = await revocationService.GetCrlAsync(issuerFingerprint, ct);
        if (crl == null) return NotFound();

        var maxAge = Math.Max(0, (int)(crl.NextUpdate - DateTime.UtcNow).TotalSeconds);
        Response.Headers.CacheControl = $"public, max-age={maxAge}";
        return File(crl.Der, "application/pkix-crl");
    }
}
