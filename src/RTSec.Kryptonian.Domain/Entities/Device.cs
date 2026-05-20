using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// Represents a medical device known to the gateway.
/// </summary>
public class Device : BaseEntity
{
    public string DisplayName { get; set; } = string.Empty;

    public string SubjectCommonName { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? SerialNumber { get; set; }

    public DeviceStatus Status { get; set; } = DeviceStatus.Pending;

    public string? ActivationCodeHash { get; set; }

    public DateTime? ActivationCodeExpiresAt { get; set; }

    public DateTime? ActivationCodeUsedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? RemovedAt { get; set; }

    public Guid? LastCertificateId { get; set; }

    public ICollection<Certificate> Certificates { get; } = new List<Certificate>();
}
