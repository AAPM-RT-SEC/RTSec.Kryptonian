using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// Represents an issued certificate.
/// </summary>
public class Certificate : BaseEntity
{
    /// <summary>
    /// Certificate serial number.
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Subject distinguished name.
    /// </summary>
    public string SubjectDn { get; set; } = string.Empty;

    /// <summary>
    /// Issuer distinguished name.
    /// </summary>
    public string IssuerDn { get; set; } = string.Empty;

    /// <summary>
    /// Certificate thumbprint (SHA-256).
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    /// Certificate validity start date.
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// Certificate validity end date.
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// PEM-encoded certificate.
    /// </summary>
    public string CertificatePem { get; set; } = string.Empty;

    /// <summary>
    /// Current certificate status.
    /// </summary>
    public CertificateStatus Status { get; set; } = CertificateStatus.Valid;

    /// <summary>
    /// Reference to the EST profile used for issuance.
    /// </summary>
    public Guid EstProfileId { get; set; }

    /// <summary>
    /// Device identifier (if provided during enrollment).
    /// </summary>
    public string? DeviceId { get; set; }

    public Guid? DeviceRecordId { get; set; }

    public Guid? CaBackendId { get; set; }

    public string? CaBackendType { get; set; }

    public string? CertificateDerBase64 { get; set; }

    public string? EncryptedPrivateKeyPem { get; set; }

    public string? GatewayOid { get; set; }

    // Navigation properties
    public EstProfile? EstProfile { get; set; }
    public Device? Device { get; set; }
    public CaBackend? CaBackend { get; set; }
}
