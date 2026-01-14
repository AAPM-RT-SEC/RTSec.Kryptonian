using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// Represents an enrollment event for audit purposes.
/// Aligned with OpenAPI.Schema.yaml EnrollmentEvent schema.
/// </summary>
public class EnrollmentEvent : BaseEntity
{
    /// <summary>
    /// Timestamp of the enrollment event.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Reference to the EST profile used.
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Device identifier (if provided).
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Enrollment status: issued, pending, rejected, error.
    /// </summary>
    public EnrollmentStatus Status { get; set; }

    /// <summary>
    /// Subject DN from the CSR.
    /// </summary>
    public string? SubjectDn { get; set; }

    /// <summary>
    /// IP address of the requestor (for audit).
    /// </summary>
    public string? RequestorIpAddress { get; set; }

    /// <summary>
    /// Error message if status is error.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Reference to the issued certificate (if successful).
    /// </summary>
    public Guid? IssuedCertificateId { get; set; }

    // Navigation properties
    public EstProfile? Profile { get; set; }
    public Certificate? IssuedCertificate { get; set; }
}
