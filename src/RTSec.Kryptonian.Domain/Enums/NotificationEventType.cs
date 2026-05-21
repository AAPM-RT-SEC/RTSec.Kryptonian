namespace RTSec.Kryptonian.Domain.Enums;

/// <summary>
/// Categories of events that may trigger an outbound notification.
/// </summary>
public enum NotificationEventType
{
    EnrollmentRejected = 1,
    CertificateNearExpiry = 2
}
