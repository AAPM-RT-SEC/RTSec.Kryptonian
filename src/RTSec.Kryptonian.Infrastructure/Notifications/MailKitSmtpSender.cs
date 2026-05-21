using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using RTSec.Kryptonian.Application.Notifications;
using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Infrastructure.Notifications;

/// <summary>
/// MailKit-backed SMTP sender. Deliberately on-prem friendly: supports anonymous /
/// Basic / NTLM auth, plain / STARTTLS / implicit TLS, and an opt-in "trust server
/// certificate" toggle for hospital infra using internal-CA or self-signed certs.
/// </summary>
public class MailKitSmtpSender : ISmtpSender
{
    private readonly ILogger<MailKitSmtpSender> _logger;

    public MailKitSmtpSender(ILogger<MailKitSmtpSender> logger)
    {
        _logger = logger;
    }

    public async Task SendAsync(SmtpDispatchConfig config, NotificationMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(message);

        if (message.Recipients.Count == 0)
        {
            _logger.LogDebug("Skipping {EventType} dispatch: no recipients", message.EventType);
            return;
        }

        using var mime = BuildMime(config, message);

        var secureSocketOption = config.TlsMode switch
        {
            SmtpTlsMode.None => SecureSocketOptions.None,
            SmtpTlsMode.StartTls => SecureSocketOptions.StartTls,
            SmtpTlsMode.Implicit => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls
        };

        using var client = new SmtpClient();
        if (config.TrustServerCertificate)
        {
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        }

        await client.ConnectAsync(config.Host, config.Port, secureSocketOption, ct);

        if (config.AuthMode == SmtpAuthMode.Basic && !string.IsNullOrEmpty(config.Username))
        {
            await client.AuthenticateAsync(config.Username, config.Password ?? string.Empty, ct);
        }
        else if (config.AuthMode == SmtpAuthMode.Ntlm && !string.IsNullOrEmpty(config.Username))
        {
            var creds = new System.Net.NetworkCredential(config.Username, config.Password ?? string.Empty);
            var ntlm = new SaslMechanismNtlm(creds);
            await client.AuthenticateAsync(ntlm, ct);
        }

        try
        {
            await client.SendAsync(mime, ct);
        }
        finally
        {
            await client.DisconnectAsync(true, ct);
        }
    }

    private static MimeMessage BuildMime(SmtpDispatchConfig config, NotificationMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(config.FromDisplayName ?? config.FromAddress, config.FromAddress));

        foreach (var r in message.Recipients)
        {
            mime.To.Add(new MailboxAddress(r.DisplayName ?? r.Email, r.Email));
        }

        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };
        return mime;
    }
}
