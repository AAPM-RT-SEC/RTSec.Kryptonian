namespace RTSec.Kryptonian.Application.Notifications;

/// <summary>
/// Transport-level SMTP sender. Implementation lives in Infrastructure (MailKit) so
/// the Application layer stays library-agnostic.
/// </summary>
public interface ISmtpSender
{
    /// <summary>
    /// Delivers a message using the supplied SMTP config. Throws on transport failure
    /// so callers can decide whether to log-and-swallow (production) or surface
    /// (test-connection endpoint).
    /// </summary>
    Task SendAsync(SmtpDispatchConfig config, NotificationMessage message, CancellationToken ct = default);
}
