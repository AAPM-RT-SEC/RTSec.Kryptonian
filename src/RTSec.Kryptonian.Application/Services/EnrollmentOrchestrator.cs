using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Application.Notifications;
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
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ILogger<EnrollmentOrchestrator> _logger;

    public EnrollmentOrchestrator(
        IUnitOfWork unitOfWork,
        ICaConnectorFactory connectorFactory,
        IPkcsService pkcsService,
        INotificationDispatcher notificationDispatcher,
        ILogger<EnrollmentOrchestrator> logger)
    {
        _unitOfWork = unitOfWork;
        _connectorFactory = connectorFactory;
        _pkcsService = pkcsService;
        _notificationDispatcher = notificationDispatcher;
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

        // The profile owns its issuer. Resolve it by id rather than falling back to
        // whichever backend happens to be globally active, so /cacerts always returns
        // the CA that will actually sign for this profile.
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
        var hasBasicDeviceId = Guid.TryParseExact(deviceId, "D", out var basicDeviceId);
        Device? device;
        if (hasBasicDeviceId)
        {
            device = await _unitOfWork.Devices.GetByIdAsync(basicDeviceId, ct);
        }
        else
        {
            device = string.IsNullOrWhiteSpace(subjectCommonName)
                ? null
                : await _unitOfWork.Devices.GetBySubjectCommonNameAsync(subjectCommonName, ct);
        }

        if (device == null && hasBasicDeviceId)
        {
            return EnrollmentResult.Failed("Invalid activation credentials", 403);
        }

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
                ct);
            if (activationError != null)
            {
                return await RejectEnrollmentAsync(profileId, device, csr.SubjectDn, clientIp, activationError, ct);
            }

            if (!_pkcsService.ValidateCsrSignature(csr))
            {
                _logger.LogWarning("CSR signature validation failed");
                return EnrollmentResult.Failed("CSR signature validation failed", 400);
            }

            activationEnrollment = true;
            if (IsPlaceholderSubjectCommonName(device))
            {
                device.SubjectCommonName = subjectCommonName!.Trim();
            }
            device.Manufacturer ??= NormalizeOptional(activationManufacturer);
            device.Model ??= NormalizeOptional(activationModel);
            device.SerialNumber ??= NormalizeOptional(activationSerialNumber);

            if (!await TryConsumeActivationCodeAsync(device, ct))
            {
                _logger.LogWarning("Activation code was already consumed for device {DeviceId}", device.Id);
                return EnrollmentResult.Failed("Activation code has already been used", 403);
            }
        }
        else
        {
            return await RejectEnrollmentAsync(
                profileId,
                device,
                csr.SubjectDn,
                clientIp,
                "Public enrollment requires pending device activation; active devices must use authenticated re-enrollment",
                ct);
        }

        return await IssueForDeviceAsync(
            profileId,
            profile,
            device,
            csr,
            clientIp,
            activationEnrollment,
            ct,
            csrSignatureValidated: activationEnrollment);
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> EnrollAdminAsync(
        Guid profileId,
        Guid deviceRecordId,
        byte[] csrBytes,
        string? clientIp,
        CancellationToken ct = default)
    {
        var profile = await _unitOfWork.EstProfiles.GetByIdAsync(profileId, ct);
        if (profile == null)
        {
            return EnrollmentResult.Failed($"EST profile not found: {profileId}", 404);
        }

        if (!profile.IsEnabled)
        {
            return EnrollmentResult.Failed($"EST profile is disabled: {profileId}", 403);
        }

        var device = await _unitOfWork.Devices.GetByIdAsync(deviceRecordId, ct);
        if (device == null)
        {
            return EnrollmentResult.Failed("Device not found", 404);
        }

        if (device.Status != DeviceStatus.Active || device.RemovedAt != null)
        {
            return EnrollmentResult.Failed("Device is not active", 403);
        }

        ParsedCsr csr;
        try
        {
            csr = _pkcsService.ParsePkcs10(csrBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse admin enrollment CSR");
            return EnrollmentResult.Failed($"Invalid CSR: {ex.Message}", 400);
        }

        if (!_pkcsService.ValidateCsrSignature(csr))
        {
            _logger.LogWarning("CSR signature validation failed");
            return EnrollmentResult.Failed("CSR signature validation failed", 400);
        }

        var csrCommonName = ExtractSubjectCommonName(csr.SubjectDn);
        if (!string.Equals(csrCommonName?.Trim(), device.SubjectCommonName, StringComparison.OrdinalIgnoreCase))
        {
            return EnrollmentResult.Failed("CSR subject common name does not match the device", 403);
        }

        return await IssueForDeviceAsync(profileId, profile, device, csr, clientIp, activationEnrollment: false, ct: ct, csrSignatureValidated: true);
    }

    private async Task<EnrollmentResult> IssueForDeviceAsync(
        Guid profileId,
        EstProfile profile,
        Device device,
        ParsedCsr csr,
        string? clientIp,
        bool activationEnrollment,
        CancellationToken ct,
        bool csrSignatureValidated = false)
    {
        // Route issuance through the backend this EST profile is bound to. The globally
        // active backend is an admin/dashboard concept; using it here would let one
        // profile silently issue from another profile's CA.
        var (backend, backendError) = await ResolveProfileBackendAsync(profile, ct);
        if (backendError != null)
        {
            return backendError;
        }

        // Validate CSR signature
        if (!csrSignatureValidated && !_pkcsService.ValidateCsrSignature(csr))
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
        if (!IsPlaceholderSubjectCommonName(device) &&
            !string.Equals(normalizedCn, device.SubjectCommonName, StringComparison.OrdinalIgnoreCase))
        {
            return "CSR subject common name does not match the registered device";
        }

        var existing = await _unitOfWork.Devices.GetBySubjectCommonNameAsync(normalizedCn, ct);
        if (existing != null && existing.Id != device.Id)
        {
            return $"A device with subject common name '{normalizedCn}' already exists";
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

    private static bool IsPlaceholderSubjectCommonName(Device device) =>
        string.Equals(device.SubjectCommonName, $"pending-{device.Id:N}", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> TryConsumeActivationCodeAsync(Device device, CancellationToken ct)
    {
        device.ActivationCodeUsedAt = DateTime.UtcNow;
        device.ActivationCodeHash = null;
        device.ActivationCodeExpiresAt = null;
        _unitOfWork.Devices.Update(device);

        return await _unitOfWork.TryConsumeActivationCodeAsync(device, ct);
    }

    /// <summary>
    /// Resolves the CA backend an EST profile is bound to via <see cref="EstProfile.CaBackendId"/>.
    /// Returns a failed <see cref="EnrollmentResult"/> in the second slot when the backend is
    /// missing or disabled, so callers can surface it directly. The globally active backend is
    /// never consulted: enrollment must be issued by the CA the profile points at.
    /// </summary>
    private async Task<(CaBackend? Backend, EnrollmentResult? Error)> ResolveProfileBackendAsync(
        EstProfile profile,
        CancellationToken ct)
    {
        var backend = await _unitOfWork.CaBackends.GetByIdAsync(profile.CaBackendId, ct);
        if (backend == null)
        {
            _logger.LogError(
                "EST profile {ProfileId} references a CA backend that does not exist: {BackendId}",
                profile.Id, profile.CaBackendId);
            return (null, EnrollmentResult.Failed("CA backend configured for this EST profile not found", 503));
        }

        if (!backend.IsEnabled)
        {
            _logger.LogWarning(
                "CA backend {BackendId} bound to EST profile {ProfileId} is disabled",
                backend.Id, profile.Id);
            return (null, EnrollmentResult.Failed("CA backend configured for this EST profile is disabled", 503));
        }

        return (backend, null);
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

        var existingSubjectCn = ExtractSubjectCommonName(existingCert.Subject);

        var now = DateTime.UtcNow;
        if (existingCert.NotAfter.ToUniversalTime() <= now)
        {
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Client certificate has expired", 403, ct);
        }

        if (existingCert.NotBefore.ToUniversalTime() > now)
        {
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Client certificate is not yet valid", 403, ct);
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
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                $"EST profile is disabled: {profileId}", 403, ct);
        }

        var existingThumbprint = NormalizeThumbprint(existingCert.GetCertHashString());
        var existingDbCert = await _unitOfWork.Certificates.GetByThumbprintAsync(existingThumbprint, ct);

        if (existingDbCert == null)
        {
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Certificate is not registered for re-enrollment", 403, ct);
        }

        if (!MatchesStoredCertificate(existingDbCert, existingCert))
        {
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Presented certificate does not match the registered certificate", 403, ct);
        }

        if (existingDbCert.EstProfileId != profileId)
        {
            _logger.LogWarning("Re-enrollment attempted with certificate from different profile. " +
                "Cert profile: {CertProfile}, Request profile: {RequestProfile}",
                existingDbCert.EstProfileId, profileId);
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Certificate does not belong to this EST profile", 403, ct);
        }

        if (existingDbCert.Status != CertificateStatus.Valid)
        {
            var reason = existingDbCert.Status == CertificateStatus.Revoked
                ? "Certificate has been revoked"
                : "Certificate is not valid for re-enrollment";
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                reason, 403, ct);
        }

        if (existingDbCert.NotAfter <= now)
        {
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Certificate has expired", 403, ct);
        }

        if (existingDbCert.NotBefore > now)
        {
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Certificate is not yet valid", 403, ct);
        }

        if (!existingDbCert.DeviceRecordId.HasValue)
        {
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Certificate does not map to a registered device", 403, ct);
        }

        var device = await _unitOfWork.Devices.GetByIdAsync(existingDbCert.DeviceRecordId.Value, ct);
        if (device == null)
        {
            return await RejectReenrollmentAsync(existingCert.Subject, existingSubjectCn, clientIp,
                "Certificate does not map to an active device", 403, ct);
        }

        if (device.Status != DeviceStatus.Active || device.RemovedAt != null)
        {
            _logger.LogWarning("Re-enrollment attempted for device {DeviceId} with status {Status}",
                device.Id, device.Status);
            return await RejectReenrollmentAsync(existingCert.Subject, device.SubjectCommonName, clientIp,
                "Device is not active", 403, ct);
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
            return await RejectReenrollmentAsync(csr.SubjectDn, device.SubjectCommonName, clientIp,
                "CSR subject common name does not match the existing device", 403, ct);
        }

        var (backend, backendError) = await ResolveProfileBackendAsync(profile, ct);
        if (backendError != null)
        {
            return backendError;
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

    private static bool MatchesStoredCertificate(Certificate certificate, X509Certificate2 presentedCertificate)
    {
        if (string.IsNullOrWhiteSpace(certificate.CertificateDerBase64))
        {
            return true;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(certificate.CertificateDerBase64),
                presentedCertificate.RawData);
        }
        catch (FormatException)
        {
            return false;
        }
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

        await _notificationDispatcher.NotifyEnrollmentRejectedAsync(
            new EnrollmentRejectionContext(
                Operation: "Enrollment",
                SubjectDn: subjectDn,
                DeviceId: device?.SubjectCommonName,
                ClientIp: clientIp,
                Reason: reason,
                OccurredAtUtc: DateTime.UtcNow),
            ct);

        return EnrollmentResult.Failed(reason, 403);
    }

    private async Task<EnrollmentResult> RejectReenrollmentAsync(
        string subjectDn,
        string? deviceId,
        string? clientIp,
        string reason,
        int statusCode,
        CancellationToken ct)
    {
        _logger.LogWarning("Rejected re-enrollment for subject {SubjectDn}: {Reason}", subjectDn, reason);

        await _notificationDispatcher.NotifyEnrollmentRejectedAsync(
            new EnrollmentRejectionContext(
                Operation: "Re-enrollment",
                SubjectDn: subjectDn,
                DeviceId: deviceId,
                ClientIp: clientIp,
                Reason: reason,
                OccurredAtUtc: DateTime.UtcNow),
            ct);

        return EnrollmentResult.Failed(reason, statusCode);
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
