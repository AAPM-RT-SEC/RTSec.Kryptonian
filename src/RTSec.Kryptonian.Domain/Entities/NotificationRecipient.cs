namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// A single email recipient for gateway notifications. Each recipient opts in
/// independently per event type so security and ops teams can be routed separately.
/// </summary>
public class NotificationRecipient : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool NotifyOnEnrollmentRejected { get; set; }
    public bool NotifyOnCertificateNearExpiry { get; set; }
}
