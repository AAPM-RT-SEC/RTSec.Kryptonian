using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Application.Notifications;

/// <summary>
/// A single addressable email destination.
/// </summary>
public record NotificationAddress(string Email, string? DisplayName = null);

/// <summary>
/// An outbound notification, transport-agnostic.
/// </summary>
public record NotificationMessage(
    NotificationEventType EventType,
    string Subject,
    string Body,
    IReadOnlyList<NotificationAddress> Recipients);

/// <summary>
/// Resolved SMTP configuration handed to <see cref="ISmtpSender"/>. Plain-text password
/// only ever lives in memory at send time, never in this record at rest.
/// </summary>
public record SmtpDispatchConfig(
    string Host,
    int Port,
    SmtpTlsMode TlsMode,
    SmtpAuthMode AuthMode,
    string? Username,
    string? Password,
    string FromAddress,
    string? FromDisplayName,
    bool TrustServerCertificate);
