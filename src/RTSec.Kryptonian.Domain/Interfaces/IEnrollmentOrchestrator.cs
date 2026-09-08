using System.Security.Cryptography.X509Certificates;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Domain.Interfaces;

/// <summary>
/// Orchestrates enrollment operations.
/// </summary>
public interface IEnrollmentOrchestrator
{
    /// <summary>
    /// Gets the CA certificates for a profile in PKCS#7 format.
    /// </summary>
    Task<byte[]> GetCaCertsAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>
    /// Enrolls a device with a new certificate.
    /// </summary>
    Task<EnrollmentResult> EnrollAsync(
        Guid profileId,
        byte[] csrBytes,
        string? deviceId,
        string? clientIp,
        CancellationToken ct = default,
        string? activationCode = null,
        string? activationManufacturer = null,
        string? activationModel = null,
        string? activationSerialNumber = null);

    /// <summary>
    /// Issues a certificate for an already-authorized device record.
    /// This is for the authenticated local admin workflow, not public EST.
    /// </summary>
    Task<EnrollmentResult> EnrollAdminAsync(
        Guid profileId,
        Guid deviceRecordId,
        byte[] csrBytes,
        string? clientIp,
        CancellationToken ct = default);

    /// <summary>
    /// Re-enrolls a device (renewal) using existing certificate for auth.
    /// </summary>
    Task<EnrollmentResult> ReenrollAsync(
        Guid profileId,
        byte[] csrBytes,
        X509Certificate2 existingCert,
        string? clientIp,
        CancellationToken ct = default);
}
