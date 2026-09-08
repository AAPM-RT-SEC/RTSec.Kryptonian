using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Api.Controllers;

[ApiController]
[Route("api/certificates")]
[Authorize(Policy = "DeviceAdmin")]
public class CertificatesController(ICertificateRevocationService revocationService) : ControllerBase
{
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeCertificateRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<RevocationReason>(request.Reason, ignoreCase: true, out var reason) || !Enum.IsDefined(reason) || reason == RevocationReason.RemoveFromCrl)
            return BadRequest(new { error = "Invalid revocation reason" });

        var result = await revocationService.RevokeAsync(id, reason, ct);
        return result.Success
            ? Ok(new { status = "revoked" })
            : StatusCode(result.StatusCode, new { error = result.ErrorMessage });
    }
}

public sealed class RevokeCertificateRequest
{
    public string Reason { get; set; } = nameof(RevocationReason.Unspecified);
}
