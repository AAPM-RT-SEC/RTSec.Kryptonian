using System.Security.Cryptography;
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

        var backend = await _unitOfWork.CaBackends.GetActiveAsync(ct)
            ?? await _unitOfWork.CaBackends.GetByIdAsync(profile.CaBackendId, ct);
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
        CancellationToken ct = default,
        string? activationCode = null,
        string? activationManufacturer = null,
        string? activationModel = null,
        string? activationSerialNumber = null)
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

        var subjectCommonName = ExtractSubjectCommonName(csr.SubjectDn);
        var device = string.IsNullOrWhiteSpace(subjectCommonName)
            ? null
            : await _unitOfWork.Devices.GetBySubjectCommonNameAsync(subjectCommonName, ct);

        if (device == null)
        {
            device = await FindPendingDeviceByActivationCodeAsync(activationCode, ct);
            if (device == null)
            {
                return await RejectEnrollmentAsync(profileId, null, csr.SubjectDn, clientIp, "Unknown device subject common name", ct);
            }
        }

        var activationEnrollment = false;
        if (device.Status == DeviceStatus.Pending)
        {
            var activationError = await ValidateActivationRequestAsync(
                device,
                activationCode,
                subjectCommonName,
                activationSerialNumber,
                ct);
            if (activationError != null)
            {
                return await RejectEnrollmentAsync(profileId, device, csr.SubjectDn, clientIp, activationError, ct);
            }

            activationEnrollment = true;
            device.SubjectCommonName = subjectCommonName!.Trim();
            device.Manufacturer = NormalizeOptional(activationManufacturer);
            device.Model = NormalizeOptional(activationModel);
            device.SerialNumber = NormalizeOptional(activationSerialNumber)!;
        }
        else if (device.Status != DeviceStatus.Active)
        {
            return await RejectEnrollmentAsync(profileId, device, csr.SubjectDn, clientIp, $"Device is {device.Status.ToString().ToLowerInvariant()}", ct);
        }

        // Validate active CA backend. EST profiles keep legacy metadata, but device enrollment routes to the active backend.
        var backend = await _unitOfWork.CaBackends.GetActiveAsync(ct);
        if (backend == null)
        {
            _logger.LogError("No active CA backend configured for profile {ProfileId}", profileId);
            return EnrollmentResult.Failed("No active CA backend configured", 503);
        }

        if (!backend.IsEnabled)
        {
            _logger.LogWarning("Active CA backend is disabled: {BackendId}", backend.Id);
            return EnrollmentResult.Failed("Active CA backend is disabled", 503);
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
            DeviceId = device.SubjectCommonName,
            DeviceRecordId = device.Id,
            CaBackendId = backend.Id,
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
                DeviceId = device.SubjectCommonName,
                DeviceRecordId = device.Id,
                CaBackendId = backend.Id,
                CaBackendType = backend.Type.ToString().ToLowerInvariant(),
                CertificateDerBase64 = Convert.ToBase64String(issuanceResult.Certificate.RawData),
                GatewayOid = ExtractGatewayOid(issuanceResult.Certificate),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _unitOfWork.Certificates.Add(certificate);
            device.LastCertificateId = certificate.Id;
            if (activationEnrollment)
            {
                device.Status = DeviceStatus.Active;
                device.ApprovedAt = DateTime.UtcNow;
                device.ActivationCodeUsedAt = DateTime.UtcNow;
                device.ActivationCodeHash = null;
                device.ActivationCodeExpiresAt = null;
            }
            _unitOfWork.Devices.Update(device);

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

    private async Task<Device?> FindPendingDeviceByActivationCodeAsync(string? activationCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(activationCode))
        {
            return null;
        }

        var devices = await _unitOfWork.Devices.GetAllAsync(ct);
        foreach (var candidate in devices.Where(d => d.Status == DeviceStatus.Pending))
        {
            if (string.IsNullOrWhiteSpace(candidate.ActivationCodeHash) || candidate.ActivationCodeUsedAt != null)
            {
                continue;
            }

            var expectedHash = DeviceService.HashActivationCode(activationCode, candidate.Id);
            if (CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHash),
                Convert.FromHexString(candidate.ActivationCodeHash)))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task<string?> ValidateActivationRequestAsync(
        Device device,
        string? activationCode,
        string? subjectCommonName,
        string? serialNumber,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(activationCode))
        {
            return "Device is pending and requires an activation code";
        }

        if (string.IsNullOrWhiteSpace(subjectCommonName))
        {
            return "Activation requires a CSR subject common name";
        }

        var normalizedCn = subjectCommonName.Trim();
        var existing = await _unitOfWork.Devices.GetBySubjectCommonNameAsync(normalizedCn, ct);
        if (existing != null && existing.Id != device.Id)
        {
            return $"A device with subject common name '{normalizedCn}' already exists";
        }

        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return "Activation requires a device serial number";
        }

        if (string.IsNullOrWhiteSpace(device.ActivationCodeHash))
        {
            return "No activation code has been generated for this device";
        }

        if (device.ActivationCodeUsedAt != null)
        {
            return "Activation code has already been used";
        }

        if (device.ActivationCodeExpiresAt == null || device.ActivationCodeExpiresAt <= DateTime.UtcNow)
        {
            return "Activation code has expired";
        }

        var expectedHash = DeviceService.HashActivationCode(activationCode, device.Id);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedHash),
            Convert.FromHexString(device.ActivationCodeHash))
            ? null
            : "Invalid activation code";
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

        var now = DateTime.UtcNow;
        if (existingCert.NotAfter.ToUniversalTime() <= now)
        {
            return EnrollmentResult.Failed("Client certificate has expired", 403);
        }

        if (existingCert.NotBefore.ToUniversalTime() > now)
        {
            return EnrollmentResult.Failed("Client certificate is not yet valid", 403);
        }

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

        var existingThumbprint = NormalizeThumbprint(existingCert.GetCertHashString());
        var existingDbCert = await _unitOfWork.Certificates.GetByThumbprintAsync(existingThumbprint, ct)
            ?? await _unitOfWork.Certificates.GetBySerialNumberAsync(existingCert.SerialNumber, ct);

        if (existingDbCert == null)
        {
            _logger.LogWarning("Re-enrollment attempted with certificate not registered in gateway: {Serial}",
                existingCert.SerialNumber);
            return EnrollmentResult.Failed("Certificate is not registered for re-enrollment", 403);
        }

        if (existingDbCert.EstProfileId != profileId)
        {
            _logger.LogWarning("Re-enrollment attempted with certificate from different profile. " +
                "Cert profile: {CertProfile}, Request profile: {RequestProfile}",
                existingDbCert.EstProfileId, profileId);
            return EnrollmentResult.Failed("Certificate does not belong to this EST profile", 403);
        }

        if (existingDbCert.Status != CertificateStatus.Valid)
        {
            var reason = existingDbCert.Status == CertificateStatus.Revoked
                ? "Certificate has been revoked"
                : "Certificate is not valid for re-enrollment";
            _logger.LogWarning("Re-enrollment attempted with {Status} certificate: {Serial}",
                existingDbCert.Status, existingCert.SerialNumber);
            return EnrollmentResult.Failed(reason, 403);
        }

        if (existingDbCert.NotAfter <= now)
        {
            return EnrollmentResult.Failed("Certificate has expired", 403);
        }

        if (existingDbCert.NotBefore > now)
        {
            return EnrollmentResult.Failed("Certificate is not yet valid", 403);
        }

        var device = await ResolveDeviceForCertificateAsync(existingDbCert, existingCert, ct);
        if (device == null)
        {
            _logger.LogWarning("Re-enrollment certificate {Serial} does not map to an active device record",
                existingCert.SerialNumber);
            return EnrollmentResult.Failed("Certificate does not map to an active device", 403);
        }

        if (device.Status != DeviceStatus.Active || device.RemovedAt != null)
        {
            _logger.LogWarning("Re-enrollment attempted for device {DeviceId} with status {Status}",
                device.Id, device.Status);
            return EnrollmentResult.Failed("Device is not active", 403);
        }

        ParsedCsr csr;
        try
        {
            csr = _pkcsService.ParsePkcs10(csrBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse re-enrollment CSR");
            return EnrollmentResult.Failed($"Invalid CSR: {ex.Message}", 400);
        }

        var csrCommonName = ExtractSubjectCommonName(csr.SubjectDn);
        if (string.IsNullOrWhiteSpace(csrCommonName))
        {
            return EnrollmentResult.Failed("CSR subject common name is required for re-enrollment", 400);
        }

        if (!string.Equals(csrCommonName.Trim(), device.SubjectCommonName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Re-enrollment CSR CN mismatch for device {DeviceId}. Existing CN: {ExistingCn}, CSR CN: {CsrCn}",
                device.Id, device.SubjectCommonName, csrCommonName);
            return EnrollmentResult.Failed("CSR subject common name does not match the existing device", 403);
        }

        var backend = await _unitOfWork.CaBackends.GetActiveAsync(ct);
        if (backend == null)
        {
            _logger.LogError("No active CA backend configured for profile {ProfileId}", profileId);
            return EnrollmentResult.Failed("No active CA backend configured", 503);
        }

        if (!backend.IsEnabled)
        {
            _logger.LogWarning("Active CA backend is disabled: {BackendId}", backend.Id);
            return EnrollmentResult.Failed("Active CA backend is disabled", 503);
        }

        if (!_pkcsService.ValidateCsrSignature(csr))
        {
            _logger.LogWarning("Re-enrollment CSR signature validation failed");
            return EnrollmentResult.Failed("CSR signature validation failed", 400);
        }

        var enrollmentEvent = new EnrollmentEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ProfileId = profileId,
            DeviceId = device.SubjectCommonName,
            DeviceRecordId = device.Id,
            CaBackendId = backend.Id,
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
            var connector = _connectorFactory.CreateConnector(backend);
            var issuanceResult = await connector.IssueCertificateAsync(csr, profile, ct);

            if (!issuanceResult.Success)
            {
                enrollmentEvent.Status = EnrollmentStatus.Error;
                enrollmentEvent.ErrorMessage = issuanceResult.ErrorMessage;
                enrollmentEvent.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.EnrollmentEvents.Update(enrollmentEvent);
                await _unitOfWork.SaveChangesAsync(ct);

                _logger.LogWarning("Certificate re-issuance failed: {Error}", issuanceResult.ErrorMessage);
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

            var certificate = new Certificate
            {
                Id = Guid.NewGuid(),
                SerialNumber = issuanceResult.Certificate.SerialNumber,
                SubjectDn = issuanceResult.Certificate.Subject,
                IssuerDn = issuanceResult.Certificate.Issuer,
                Thumbprint = NormalizeThumbprint(issuanceResult.Certificate.GetCertHashString()),
                NotBefore = issuanceResult.Certificate.NotBefore.ToUniversalTime(),
                NotAfter = issuanceResult.Certificate.NotAfter.ToUniversalTime(),
                CertificatePem = _pkcsService.ExportToPem(issuanceResult.Certificate),
                Status = CertificateStatus.Valid,
                EstProfileId = profileId,
                DeviceId = device.SubjectCommonName,
                DeviceRecordId = device.Id,
                CaBackendId = backend.Id,
                CaBackendType = backend.Type.ToString().ToLowerInvariant(),
                CertificateDerBase64 = Convert.ToBase64String(issuanceResult.Certificate.RawData),
                GatewayOid = ExtractGatewayOid(issuanceResult.Certificate),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _unitOfWork.Certificates.Add(certificate);
            device.LastCertificateId = certificate.Id;
            _unitOfWork.Devices.Update(device);

            enrollmentEvent.Status = EnrollmentStatus.Issued;
            enrollmentEvent.IssuedCertificateId = certificate.Id;
            enrollmentEvent.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.EnrollmentEvents.Update(enrollmentEvent);

            await _unitOfWork.SaveChangesAsync(ct);

            var certChain = issuanceResult.CertificateChain?.ToArray() ?? new[] { issuanceResult.Certificate! };
            var pkcs7 = _pkcsService.EncodeToPkcs7(certChain);
            var responseBody = _pkcsService.EncodeEstResponseBody(pkcs7);

            _logger.LogInformation("Certificate re-issued successfully for profile {ProfileId}, serial {Serial}",
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

            _logger.LogError(ex, "Re-enrollment failed for profile {ProfileId}", profileId);
            return EnrollmentResult.Failed($"Enrollment failed: {ex.Message}", 500);
        }
    }

    private async Task<Device?> ResolveDeviceForCertificateAsync(
        Certificate certificate,
        X509Certificate2 presentedCertificate,
        CancellationToken ct)
    {
        if (certificate.DeviceRecordId.HasValue)
        {
            var deviceById = await _unitOfWork.Devices.GetByIdAsync(certificate.DeviceRecordId.Value, ct);
            if (deviceById != null)
            {
                return deviceById;
            }
        }

        var commonName = ExtractSubjectCommonName(certificate.SubjectDn)
            ?? (!string.IsNullOrWhiteSpace(certificate.DeviceId) ? certificate.DeviceId : null)
            ?? ExtractSubjectCommonName(presentedCertificate.Subject);

        return string.IsNullOrWhiteSpace(commonName)
            ? null
            : await _unitOfWork.Devices.GetBySubjectCommonNameAsync(commonName, ct);
    }

    private async Task<EnrollmentResult> RejectEnrollmentAsync(
        Guid profileId,
        Device? device,
        string subjectDn,
        string? clientIp,
        string reason,
        CancellationToken ct)
    {
        var enrollmentEvent = new EnrollmentEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ProfileId = profileId,
            DeviceId = device?.SubjectCommonName,
            DeviceRecordId = device?.Id,
            SubjectDn = subjectDn,
            RequestorIpAddress = clientIp,
            Status = EnrollmentStatus.Rejected,
            ErrorMessage = reason,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _unitOfWork.EnrollmentEvents.Add(enrollmentEvent);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogWarning("Rejected enrollment for subject {SubjectDn}: {Reason}", subjectDn, reason);
        return EnrollmentResult.Failed(reason, 403);
    }

    private static string? ExtractSubjectCommonName(string subjectDn)
    {
        if (string.IsNullOrWhiteSpace(subjectDn))
        {
            return null;
        }

        var parts = subjectDn.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return part[3..].Trim();
            }
        }

        return null;
    }

    private static string? ExtractGatewayOid(X509Certificate2 certificate)
    {
        const string gatewayOidPrefix = "1.3.6.1.4.1.99999.";
        foreach (var extension in certificate.Extensions)
        {
            var oid = extension.Oid?.Value;
            if (!string.IsNullOrWhiteSpace(oid) && oid.StartsWith(gatewayOidPrefix, StringComparison.Ordinal))
            {
                return oid;
            }
        }

        return null;
    }

    private static string NormalizeThumbprint(string thumbprint) =>
        thumbprint.Replace(":", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
