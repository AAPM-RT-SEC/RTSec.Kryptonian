using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>
/// Orchestrates EST enrollment operations.
/// Coordinates between profiles, CA connectors, and repositories.
/// </summary>
public class EnrollmentOrchestrator : IEnrollmentOrchestrator
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICaConnectorFactory _connectorFactory;
    private readonly IPkcsService _pkcsService;
    private readonly ILogger<EnrollmentOrchestrator> _logger;

    public EnrollmentOrchestrator(
        IUnitOfWork unitOfWork,
        ICaConnectorFactory connectorFactory,
        IPkcsService pkcsService,
        ILogger<EnrollmentOrchestrator> logger)
    {
        _unitOfWork = unitOfWork;
        _connectorFactory = connectorFactory;
        _pkcsService = pkcsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]> GetCaCertsAsync(Guid profileId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting CA certificates for profile {ProfileId}", profileId);

        var profile = await _unitOfWork.EstProfiles.GetByIdAsync(profileId, ct);
        if (profile == null)
        {
            throw new InvalidOperationException($"EST profile not found: {profileId}");
        }

        if (!profile.IsEnabled)
        {
            throw new InvalidOperationException($"EST profile is disabled: {profileId}");
        }

        var backend = await _unitOfWork.CaBackends.GetByIdAsync(profile.CaBackendId, ct);
        if (backend == null)
        {
            throw new InvalidOperationException($"CA backend not found: {profile.CaBackendId}");
        }

        if (!backend.IsEnabled)
        {
            throw new InvalidOperationException($"CA backend is disabled: {backend.Id}");
        }

        var connector = _connectorFactory.CreateConnector(backend);
        var caCerts = await connector.GetCaCertificatesAsync(ct);

        if (caCerts.Length == 0)
        {
            throw new InvalidOperationException("No CA certificates available");
        }

        // Encode to PKCS#7 for EST response
        var pkcs7 = _pkcsService.EncodeToPkcs7(caCerts);

        _logger.LogDebug("Returning {Count} CA certificates for profile {ProfileId}",
            caCerts.Length, profileId);

        return pkcs7;
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> EnrollAsync(
        Guid profileId,
        byte[] csrBytes,
        string? deviceId,
        string? clientIp,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting enrollment for profile {ProfileId}, device {DeviceId}",
            profileId, deviceId ?? "unknown");

        // Validate profile
        var profile = await _unitOfWork.EstProfiles.GetByIdAsync(profileId, ct);
        if (profile == null)
        {
            _logger.LogWarning("EST profile not found: {ProfileId}", profileId);
            return EnrollmentResult.Failed($"EST profile not found: {profileId}", 404);
        }

        if (!profile.IsEnabled)
        {
            _logger.LogWarning("EST profile is disabled: {ProfileId}", profileId);
            return EnrollmentResult.Failed($"EST profile is disabled: {profileId}", 403);
        }

        // Validate CA backend
        var backend = await _unitOfWork.CaBackends.GetByIdAsync(profile.CaBackendId, ct);
        if (backend == null)
        {
            _logger.LogError("CA backend not found for profile {ProfileId}: {BackendId}",
                profileId, profile.CaBackendId);
            return EnrollmentResult.Failed("CA backend configuration error", 500);
        }

        if (!backend.IsEnabled)
        {
            _logger.LogWarning("CA backend is disabled: {BackendId}", backend.Id);
            return EnrollmentResult.Failed("CA backend is disabled", 503);
        }

        // Parse and validate CSR
        ParsedCsr csr;
        try
        {
            csr = _pkcsService.ParsePkcs10(csrBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse CSR");
            return EnrollmentResult.Failed($"Invalid CSR: {ex.Message}", 400);
        }

        // Validate CSR signature
        if (!_pkcsService.ValidateCsrSignature(csr))
        {
            _logger.LogWarning("CSR signature validation failed");
            return EnrollmentResult.Failed("CSR signature validation failed", 400);
        }

        // Create enrollment event for tracking
        var enrollmentEvent = new EnrollmentEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ProfileId = profileId,
            DeviceId = deviceId,
            SubjectDn = csr.SubjectDn,
            RequestorIpAddress = clientIp,
            Status = EnrollmentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _unitOfWork.EnrollmentEvents.Add(enrollmentEvent);
        await _unitOfWork.SaveChangesAsync(ct);

        try
        {
            // Issue certificate
            var connector = _connectorFactory.CreateConnector(backend);
            var issuanceResult = await connector.IssueCertificateAsync(csr, profile, ct);

            if (!issuanceResult.Success)
            {
                enrollmentEvent.Status = EnrollmentStatus.Error;
                enrollmentEvent.ErrorMessage = issuanceResult.ErrorMessage;
                enrollmentEvent.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.EnrollmentEvents.Update(enrollmentEvent);
                await _unitOfWork.SaveChangesAsync(ct);

                _logger.LogWarning("Certificate issuance failed: {Error}", issuanceResult.ErrorMessage);
                return EnrollmentResult.Failed(issuanceResult.ErrorMessage ?? "Certificate issuance failed", 500);
            }

            if (issuanceResult.Certificate == null)
            {
                enrollmentEvent.Status = EnrollmentStatus.Error;
                enrollmentEvent.ErrorMessage = "No certificate returned from CA";
                enrollmentEvent.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.EnrollmentEvents.Update(enrollmentEvent);
                await _unitOfWork.SaveChangesAsync(ct);

                return EnrollmentResult.Failed("No certificate returned from CA", 500);
            }

            // Store the certificate
            var certificate = new Certificate
            {
                Id = Guid.NewGuid(),
                SerialNumber = issuanceResult.Certificate.SerialNumber,
                SubjectDn = issuanceResult.Certificate.Subject,
                IssuerDn = issuanceResult.Certificate.Issuer,
                Thumbprint = issuanceResult.Certificate.GetCertHashString(),
                NotBefore = issuanceResult.Certificate.NotBefore.ToUniversalTime(),
                NotAfter = issuanceResult.Certificate.NotAfter.ToUniversalTime(),
                CertificatePem = _pkcsService.ExportToPem(issuanceResult.Certificate),
                Status = CertificateStatus.Valid,
                EstProfileId = profileId,
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _unitOfWork.Certificates.Add(certificate);

            // Update enrollment event
            enrollmentEvent.Status = EnrollmentStatus.Issued;
            enrollmentEvent.IssuedCertificateId = certificate.Id;
            enrollmentEvent.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.EnrollmentEvents.Update(enrollmentEvent);

            await _unitOfWork.SaveChangesAsync(ct);

            // Build certificate chain for response
            var certChain = issuanceResult.CertificateChain?.ToArray() ?? new[] { issuanceResult.Certificate! };
            var pkcs7 = _pkcsService.EncodeToPkcs7(certChain);
            var responseBody = _pkcsService.EncodeEstResponseBody(pkcs7);

            _logger.LogInformation("Certificate issued successfully for profile {ProfileId}, serial {Serial}",
                profileId, certificate.SerialNumber);

            return EnrollmentResult.Successful(responseBody, enrollmentEvent.Id);
        }
        catch (Exception ex)
        {
            enrollmentEvent.Status = EnrollmentStatus.Error;
            enrollmentEvent.ErrorMessage = ex.Message;
            enrollmentEvent.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.EnrollmentEvents.Update(enrollmentEvent);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogError(ex, "Enrollment failed for profile {ProfileId}", profileId);
            return EnrollmentResult.Failed($"Enrollment failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> ReenrollAsync(
        Guid profileId,
        byte[] csrBytes,
        X509Certificate2 existingCert,
        string? clientIp,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingCert);
        _logger.LogInformation("Starting re-enrollment for profile {ProfileId}, existing cert serial {Serial}",
            profileId, existingCert.SerialNumber);

        // For re-enrollment, we use the existing certificate's subject as device ID
        var deviceId = existingCert.Subject;

        // Validate that the existing certificate was issued by us (optional)
        var existingDbCert = await _unitOfWork.Certificates.GetBySerialNumberAsync(existingCert.SerialNumber, ct);
        if (existingDbCert != null)
        {
            // Validate that the certificate belongs to this profile
            if (existingDbCert.EstProfileId != profileId)
            {
                _logger.LogWarning("Re-enrollment attempted with certificate from different profile. " +
                    "Cert profile: {CertProfile}, Request profile: {RequestProfile}",
                    existingDbCert.EstProfileId, profileId);
                return EnrollmentResult.Failed("Certificate does not belong to this EST profile", 403);
            }

            // Check certificate status
            if (existingDbCert.Status == CertificateStatus.Revoked)
            {
                _logger.LogWarning("Re-enrollment attempted with revoked certificate: {Serial}",
                    existingCert.SerialNumber);
                return EnrollmentResult.Failed("Certificate has been revoked", 403);
            }
        }
        else
        {
            _logger.LogDebug("Existing certificate not found in database, allowing re-enrollment");
        }

        // Proceed with enrollment using the same logic
        return await EnrollAsync(profileId, csrBytes, deviceId, clientIp, ct);
    }
}
