using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Notifications;

/// <summary>
/// Loads current notification settings + recipients, builds messages for each event
/// type, and hands off to ISmtpSender. Exceptions on the production code paths are
/// logged and swallowed so notification failures never affect enrollment requests.
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISmtpSender _sender;
    private readonly IDataProtectionService _dataProtection;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IUnitOfWork unitOfWork,
        ISmtpSender sender,
        IDataProtectionService dataProtection,
        ILogger<NotificationDispatcher> logger)
    {
        _unitOfWork = unitOfWork;
        _sender = sender;
        _dataProtection = dataProtection;
        _logger = logger;
    }

    public async Task NotifyEnrollmentRejectedAsync(EnrollmentRejectionContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var settings = await _unitOfWork.NotificationSettings.GetSingletonAsync(ct);
            if (!IsEnabledFor(settings, NotificationEventType.EnrollmentRejected))
            {
                return;
            }

            var recipients = await ResolveRecipientsAsync(NotificationEventType.EnrollmentRejected, ct);
            if (recipients.Count == 0)
            {
                _logger.LogDebug("No recipients subscribed to enrollment rejection notifications");
                return;
            }

            var subject = $"[Kryptonian] {context.Operation} rejected for {context.SubjectDn}";
            var body =
                $"A device {context.Operation.ToLowerInvariant()} attempt was rejected.\n\n" +
                $"  Subject DN : {context.SubjectDn}\n" +
                $"  Device ID  : {context.DeviceId ?? "(unknown)"}\n" +
                $"  Client IP  : {context.ClientIp ?? "(unknown)"}\n" +
                $"  Time (UTC) : {context.OccurredAtUtc:u}\n\n" +
                $"Reason: {context.Reason}\n";

            var message = new NotificationMessage(
                NotificationEventType.EnrollmentRejected, subject, body, recipients);

            await _sender.SendAsync(BuildDispatchConfig(settings!), message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to dispatch enrollment-rejection notification for subject {SubjectDn}",
                context.SubjectDn);
        }
    }

    public async Task NotifyCertificateNearExpiryAsync(CertificateExpiryContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var settings = await _unitOfWork.NotificationSettings.GetSingletonAsync(ct);
            if (!IsEnabledFor(settings, NotificationEventType.CertificateNearExpiry))
            {
                return;
            }

            var recipients = await ResolveRecipientsAsync(NotificationEventType.CertificateNearExpiry, ct);
            if (recipients.Count == 0)
            {
                _logger.LogDebug("No recipients subscribed to certificate-expiry notifications");
                return;
            }

            var subject = $"[Kryptonian] Certificate expiring in {context.DaysUntilExpiry} day(s): {context.SubjectDn}";
            var body =
                "A device certificate is approaching expiry and has not requested renewal.\n\n" +
                $"  Subject DN : {context.SubjectDn}\n" +
                $"  Device ID  : {context.DeviceId ?? "(unknown)"}\n" +
                $"  Serial     : {context.SerialNumber}\n" +
                $"  Expires    : {context.NotAfterUtc:u}\n" +
                $"  Days left  : {context.DaysUntilExpiry}\n\n" +
                "If the device returns to the network after expiry it may be unable to " +
                "establish secure connections. Investigate device connectivity or trigger a renewal.\n";

            var message = new NotificationMessage(
                NotificationEventType.CertificateNearExpiry, subject, body, recipients);

            await _sender.SendAsync(BuildDispatchConfig(settings!), message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to dispatch certificate-expiry notification for serial {Serial}",
                context.SerialNumber);
        }
    }

    public async Task SendTestEmailAsync(SmtpDispatchConfig config, NotificationAddress recipient, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(recipient);

        var message = new NotificationMessage(
            NotificationEventType.EnrollmentRejected,
            "[Kryptonian] SMTP test email",
            "This is a test message from the Kryptonian gateway.\n\n" +
            "If you received this, your SMTP configuration is working.\n",
            new[] { recipient });

        await _sender.SendAsync(config, message, ct);
    }

    private static bool IsEnabledFor(NotificationSettings? settings, NotificationEventType type)
    {
        if (settings == null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            return false;
        }

        return type switch
        {
            NotificationEventType.EnrollmentRejected => settings.NotifyOnEnrollmentRejected,
            NotificationEventType.CertificateNearExpiry => settings.NotifyOnCertificateNearExpiry,
            _ => false
        };
    }

    private async Task<IReadOnlyList<NotificationAddress>> ResolveRecipientsAsync(
        NotificationEventType type, CancellationToken ct)
    {
        var subscribed = await _unitOfWork.NotificationRecipients.GetSubscribedToAsync(type, ct);
        return subscribed
            .Select(r => new NotificationAddress(r.Email, r.DisplayName))
            .ToList();
    }

    private SmtpDispatchConfig BuildDispatchConfig(NotificationSettings settings)
    {
        var password = string.IsNullOrEmpty(settings.EncryptedPassword)
            ? null
            : _dataProtection.Unprotect(settings.EncryptedPassword);

        return new SmtpDispatchConfig(
            Host: settings.SmtpHost,
            Port: settings.SmtpPort,
            TlsMode: settings.TlsMode,
            AuthMode: settings.AuthMode,
            Username: settings.Username,
            Password: password,
            FromAddress: settings.FromAddress,
            FromDisplayName: settings.FromDisplayName,
            TrustServerCertificate: settings.TrustServerCertificate);
    }
}
