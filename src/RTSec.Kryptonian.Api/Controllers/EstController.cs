using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Api.Controllers;

/// <summary>
/// Controller for EST protocol endpoints (RFC 7030).
/// Routes: /.well-known/est/{label}/cacerts, /simpleenroll, /simplereenroll
/// </summary>
[ApiController]
[Route(".well-known/est")]
public class EstController : ControllerBase
{
    private readonly IEnrollmentOrchestrator _orchestrator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPkcsService _pkcsService;
    private readonly ILogger<EstController> _logger;

    /// <summary>
    /// EST response content type per RFC 7030: application/pkcs7-mime; smime-type=certs-only
    /// </summary>
    private const string Pkcs7MimeType = "application/pkcs7-mime; smime-type=certs-only";

    /// <summary>
    /// EST request content type for CSR: application/pkcs10
    /// </summary>
    private const string Pkcs10MimeType = "application/pkcs10";

    /// <summary>
    /// Maximum CSR request body size (64KB).
    /// </summary>
    private const int MaxCsrBodySize = 65536;

    public EstController(
        IEnrollmentOrchestrator orchestrator,
        IUnitOfWork unitOfWork,
        IPkcsService pkcsService,
        ILogger<EstController> logger)
    {
        _orchestrator = orchestrator;
        _unitOfWork = unitOfWork;
        _pkcsService = pkcsService;
        _logger = logger;
    }

    /// <summary>
    /// EST /cacerts endpoint - Get CA certificates.
    /// Returns the CA certificate chain in PKCS#7 format.
    /// </summary>
    /// <param name="label">EST profile path prefix (optional, defaults to root)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("{label}/cacerts")]
    [HttpGet("cacerts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCaCerts(string? label, CancellationToken ct)
    {
        try
        {
            var profileId = await ResolveProfileIdAsync(label, ct);
            if (profileId == null)
            {
                _logger.LogWarning("No EST profile found for label: {Label}", label ?? "(default)");
                return EstError(StatusCodes.Status404NotFound, "EST profile not found");
            }

            var pkcs7 = await _orchestrator.GetCaCertsAsync(profileId.Value, ct);
            var responseBody = _pkcsService.EncodeEstResponseBody(pkcs7);

            Response.Headers["Content-Transfer-Encoding"] = "base64";
            return File(responseBody, Pkcs7MimeType);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to get CA certs for profile {Label}", label);
            return EstError(StatusCodes.Status404NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CA certificates");
            return EstError(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// EST /simpleenroll endpoint - Request a new certificate.
    /// Accepts PKCS#10 CSR, returns PKCS#7 certificate chain.
    /// ASP.NET Core route matching is case-insensitive, so simpleEnroll is accepted by the same routes.
    /// </summary>
    /// <param name="label">EST profile path prefix (optional)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("{label}/simpleenroll")]
    [HttpPost("simpleenroll")]
    [Consumes(Pkcs10MimeType, "text/plain")]
    [RequestSizeLimit(MaxCsrBodySize)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SimpleEnroll(string? label, CancellationToken ct)
    {
        try
        {
            var profileId = await ResolveProfileIdAsync(label, ct);
            if (profileId == null)
            {
                _logger.LogWarning("No EST profile found for label: {Label}", label ?? "(default)");
                return EstError(StatusCodes.Status404NotFound, "EST profile not found");
            }

            // Validate profile requires client cert (if configured)
            var profile = await _unitOfWork.EstProfiles.GetByIdAsync(profileId.Value, ct);
            if (profile?.RequireClientCertificate == true)
            {
                var clientCert = GetClientCertificate();
                if (clientCert == null)
                {
                    _logger.LogWarning("Client certificate required but not provided for profile {ProfileId}", profileId);
                    return EstError(StatusCodes.Status401Unauthorized, "Client certificate required");
                }

                // Validate client certificate chain if configured
                var validationResult = ValidateClientCertificate(clientCert, profile);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Client certificate validation failed for profile {ProfileId}: {Reason}",
                        profileId, validationResult.Reason);
                    return EstError(StatusCodes.Status403Forbidden, validationResult.Reason ?? "Client certificate validation failed");
                }
            }

            // Decode CSR from request body
            var contentTransferEncoding = Request.Headers["Content-Transfer-Encoding"].ToString();
            var csrBytes = await _pkcsService.DecodeEstRequestBodyAsync(
                Request.Body,
                contentTransferEncoding,
                maxSize: MaxCsrBodySize,
                cancellationToken: ct);

            // Get device ID from client cert (header fallback is informational only, logged but not trusted)
            var deviceId = GetDeviceIdentifier(profile);
            var activationCode = Request.Headers["X-Activation-Code"].ToString();
            var activationManufacturer = Request.Headers["X-Device-Manufacturer"].ToString();
            var activationModel = Request.Headers["X-Device-Model"].ToString();
            var activationSerialNumber = Request.Headers["X-Device-Serial-Number"].ToString();
            var clientIp = GetClientIp();

            var result = await _orchestrator.EnrollAsync(
                profileId.Value,
                csrBytes,
                deviceId,
                clientIp,
                ct,
                string.IsNullOrWhiteSpace(activationCode) ? null : activationCode,
                string.IsNullOrWhiteSpace(activationManufacturer) ? null : activationManufacturer,
                string.IsNullOrWhiteSpace(activationModel) ? null : activationModel,
                string.IsNullOrWhiteSpace(activationSerialNumber) ? null : activationSerialNumber);

            return HandleEnrollmentResult(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("maximum allowed size"))
        {
            _logger.LogWarning("CSR request too large");
            return EstError(StatusCodes.Status413PayloadTooLarge, "Request body too large");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid CSR request");
            return EstError(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing enrollment request");
            return EstError(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// EST /simplereenroll endpoint - Renew an existing certificate.
    /// Requires valid client certificate for authentication.
    /// ASP.NET Core route matching is case-insensitive, so simpleReenroll is accepted by the same routes.
    /// </summary>
    /// <param name="label">EST profile path prefix (optional)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("{label}/simplereenroll")]
    [HttpPost("simplereenroll")]
    [Consumes(Pkcs10MimeType, "text/plain")]
    [RequestSizeLimit(MaxCsrBodySize)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SimpleReenroll(string? label, CancellationToken ct)
    {
        try
        {
            var profileId = await ResolveProfileIdAsync(label, ct);
            if (profileId == null)
            {
                _logger.LogWarning("No EST profile found for label: {Label}", label ?? "(default)");
                return EstError(StatusCodes.Status404NotFound, "EST profile not found");
            }

            // Re-enrollment always requires a client certificate
            var clientCert = GetClientCertificate();
            if (clientCert == null)
            {
                _logger.LogWarning("Client certificate required for re-enrollment");
                return EstError(StatusCodes.Status401Unauthorized, "Client certificate required for re-enrollment");
            }

            // Validate client certificate chain if profile requires it
            var profile = await _unitOfWork.EstProfiles.GetByIdAsync(profileId.Value, ct);
            if (profile != null)
            {
                var validationResult = ValidateClientCertificate(clientCert, profile);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Client certificate validation failed for re-enrollment on profile {ProfileId}: {Reason}",
                        profileId, validationResult.Reason);
                    return EstError(StatusCodes.Status403Forbidden, validationResult.Reason ?? "Client certificate validation failed");
                }
            }

            // Decode CSR from request body
            var contentTransferEncoding = Request.Headers["Content-Transfer-Encoding"].ToString();
            var csrBytes = await _pkcsService.DecodeEstRequestBodyAsync(
                Request.Body,
                contentTransferEncoding,
                maxSize: MaxCsrBodySize,
                cancellationToken: ct);

            var clientIp = GetClientIp();

            var result = await _orchestrator.ReenrollAsync(profileId.Value, csrBytes, clientCert, clientIp, ct);

            return HandleEnrollmentResult(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("maximum allowed size"))
        {
            _logger.LogWarning("CSR request too large");
            return EstError(StatusCodes.Status413PayloadTooLarge, "Request body too large");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid re-enrollment CSR request");
            return EstError(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing re-enrollment request");
            return EstError(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Resolves the EST profile ID from the label and hostname.
    /// A label is part of the profile identity: an unlabeled request resolves against the
    /// default path prefix "/.well-known/est", and a labeled request resolves against
    /// "/.well-known/est/{label}" only. Unknown labels are rejected rather than silently
    /// falling back to the default profile, which would let a mistyped or revoked label
    /// enroll against the wrong CA.
    /// </summary>
    private async Task<Guid?> ResolveProfileIdAsync(string? label, CancellationToken ct)
    {
        var hostname = Request.Host.Host;
        var pathPrefix = string.IsNullOrEmpty(label) ? DefaultPathPrefix : $"{DefaultPathPrefix}/{label}";

        _logger.LogDebug("Resolving profile for hostname={Hostname}, path={Path}", hostname, pathPrefix);

        var profile = await _unitOfWork.EstProfiles.GetByPathAndHostnameAsync(pathPrefix, hostname, ct);
        if (profile != null && profile.IsEnabled)
        {
            return profile.Id;
        }

        return null;
    }

    /// <summary>The unlabeled EST path prefix devices use by default.</summary>
    private const string DefaultPathPrefix = "/.well-known/est";

    /// <summary>
    /// Gets the client certificate from the TLS connection.
    /// </summary>
    private X509Certificate2? GetClientCertificate()
    {
        return HttpContext.Connection.ClientCertificate;
    }

    /// <summary>
    /// Validates a client certificate against profile requirements.
    /// </summary>
    /// <param name="clientCert">The client certificate to validate.</param>
    /// <param name="profile">The EST profile with validation requirements.</param>
    /// <returns>Validation result with reason if failed.</returns>
    private (bool IsValid, string? Reason) ValidateClientCertificate(X509Certificate2 clientCert, EstProfile profile)
    {
        // Check basic validity (not expired)
        var now = DateTime.UtcNow;
        if (clientCert.NotAfter.ToUniversalTime() < now)
        {
            return (false, "Client certificate has expired");
        }

        if (clientCert.NotBefore.ToUniversalTime() > now)
        {
            return (false, "Client certificate is not yet valid");
        }

        // If chain validation is not required, accept the certificate
        if (!profile.ValidateClientCertificateChain)
        {
            return (true, null);
        }

        // Validate certificate chain against trusted CAs
        if (profile.TrustedClientCaThumbprints.Count == 0)
        {
            _logger.LogWarning("Profile {ProfileId} requires chain validation but has no trusted CA thumbprints configured",
                profile.Id);
            return (false, "No trusted CAs configured for client certificate validation");
        }

        // Build and validate the certificate chain
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Can be made configurable
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        if (!chain.Build(clientCert))
        {
            var errors = string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation));
            _logger.LogWarning("Client certificate chain build failed: {Errors}", errors);
            return (false, "Client certificate chain validation failed");
        }

        // Check if any certificate in the chain matches a trusted CA thumbprint
        var trustedThumbprints = new HashSet<string>(
            profile.TrustedClientCaThumbprints.Select(t => t.ToUpperInvariant().Replace(":", "")),
            StringComparer.OrdinalIgnoreCase);

        foreach (var chainElement in chain.ChainElements)
        {
            var thumbprint = chainElement.Certificate.GetCertHashString();
            if (trustedThumbprints.Contains(thumbprint))
            {
                _logger.LogDebug("Client certificate chains to trusted CA with thumbprint {Thumbprint}", thumbprint);
                return (true, null);
            }
        }

        _logger.LogWarning("Client certificate does not chain to any trusted CA. Issuer: {Issuer}",
            clientCert.Issuer);
        return (false, "Client certificate not issued by a trusted CA");
    }

    /// <summary>
    /// Gets device identifier from client certificate.
    /// X-Device-Id header is logged for informational purposes but not trusted for authentication.
    /// </summary>
    /// <param name="profile">The EST profile (used to determine if header fallback is logged).</param>
    private string? GetDeviceIdentifier(EstProfile? profile)
    {
        // Primary: use client certificate subject
        var clientCert = GetClientCertificate();
        if (clientCert != null)
        {
            return clientCert.Subject;
        }

        // Header fallback: log for audit but treat as untrusted/informational only
        var headerDeviceId = Request.Headers["X-Device-Id"].ToString();
        if (!string.IsNullOrEmpty(headerDeviceId))
        {
            _logger.LogDebug("Device ID from X-Device-Id header (untrusted): {DeviceId}", headerDeviceId);
            // Return null to indicate no authenticated device ID
            // The header value is only logged for correlation, not used for authorization
            return null;
        }

        return null;
    }

    /// <summary>
    /// Handles enrollment result, setting appropriate headers for pending responses.
    /// </summary>
    private IActionResult HandleEnrollmentResult(Domain.ValueObjects.EnrollmentResult result)
    {
        if (result.Success)
        {
            Response.Headers["Content-Transfer-Encoding"] = "base64";
            return File(result.Pkcs7Response!.ToArray(), Pkcs7MimeType);
        }

        // Handle pending/async enrollment (HTTP 202 with Retry-After)
        if (result.IsPending)
        {
            if (result.RetryAfterSeconds.HasValue)
            {
                Response.Headers["Retry-After"] = result.RetryAfterSeconds.Value.ToString();
            }
            return EstError(StatusCodes.Status202Accepted, "Enrollment pending", result.RetryAfterSeconds);
        }

        // Return error with appropriate status code
        return EstError(result.StatusCode, result.ErrorMessage ?? "Enrollment failed");
    }

    private static ObjectResult EstError(int statusCode, string error, int? retryAfter = null)
    {
        object body = retryAfter == null
            ? new { error }
            : new { error, retryAfter };

        ObjectResult result = statusCode switch
        {
            StatusCodes.Status400BadRequest => new BadRequestObjectResult(body),
            StatusCodes.Status401Unauthorized => new UnauthorizedObjectResult(body),
            StatusCodes.Status404NotFound => new NotFoundObjectResult(body),
            _ => new ObjectResult(body) { StatusCode = statusCode }
        };

        result.ContentTypes.Clear();
        result.ContentTypes.Add("application/json");
        return result;
    }

    /// <summary>
    /// Gets the client IP address, respecting X-Forwarded-For header.
    /// </summary>
    private string? GetClientIp()
    {
        // Check X-Forwarded-For header first (for reverse proxy scenarios)
        var forwardedFor = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (original client)
            var firstIp = forwardedFor.Split(',')[0].Trim();
            return firstIp;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
