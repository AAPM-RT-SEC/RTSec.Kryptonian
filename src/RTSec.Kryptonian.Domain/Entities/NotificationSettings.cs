using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// Singleton SMTP and event-notification configuration. Designed for on-prem deployment:
/// no cloud APIs, supports anonymous/Basic/NTLM auth and the three common TLS modes.
/// </summary>
public class NotificationSettings : BaseEntity
{
    public const int MinExpiryWarningDays = 1;
    public const int MaxExpiryWarningDays = 365;
    public const int DefaultExpiryWarningDays = 14;

    /// <summary>Master switch. When false, no notifications are dispatched regardless of per-event toggles.</summary>
    public bool Enabled { get; set; }

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public SmtpTlsMode TlsMode { get; set; } = SmtpTlsMode.StartTls;
    public SmtpAuthMode AuthMode { get; set; } = SmtpAuthMode.None;
    public string? Username { get; set; }

    /// <summary>Encrypted via IDataProtectionService. Never returned over the API.</summary>
    public string? EncryptedPassword { get; set; }

    public string FromAddress { get; set; } = string.Empty;
    public string? FromDisplayName { get; set; }

    /// <summary>
    /// When true, accept the SMTP server's TLS certificate without chain validation. Off by default;
    /// available because hospital SMTP servers routinely use internal-CA or self-signed certs.
    /// </summary>
    public bool TrustServerCertificate { get; set; }

    public bool NotifyOnEnrollmentRejected { get; set; } = true;
    public bool NotifyOnCertificateNearExpiry { get; set; } = true;

    public int ExpiryWarningDays { get; set; } = DefaultExpiryWarningDays;
}
