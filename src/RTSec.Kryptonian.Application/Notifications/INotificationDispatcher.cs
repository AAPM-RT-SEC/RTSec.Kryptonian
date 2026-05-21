using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Application.Notifications;

/// <summary>
/// Application-level facade. Builds messages for known event types, looks up subscribed
/// recipients, resolves current SMTP settings, and delegates transport to <see cref="ISmtpSender"/>.
/// All methods are safe to await on hot paths — exceptions are logged and swallowed.
/// </summary>
public interface INotificationDispatcher
{
    Task NotifyEnrollmentRejectedAsync(
        EnrollmentRejectionContext context,
        CancellationToken ct = default);

    Task NotifyCertificateNearExpiryAsync(
        CertificateExpiryContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a single test email using the supplied draft settings. Exceptions propagate
    /// so the UI can surface SMTP misconfiguration.
    /// </summary>
    Task SendTestEmailAsync(
        SmtpDispatchConfig config,
        NotificationAddress recipient,
        CancellationToken ct = default);
}

public record EnrollmentRejectionContext(
    string Operation,
    string SubjectDn,
    string? DeviceId,
    string? ClientIp,
    string Reason,
    DateTime OccurredAtUtc);

public record CertificateExpiryContext(
    string SerialNumber,
    string SubjectDn,
    string? DeviceId,
    DateTime NotAfterUtc,
    int DaysUntilExpiry);
