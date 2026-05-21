using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTSec.Kryptonian.Application.Notifications;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Tests.Services;

public class NotificationDispatcherTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<INotificationSettingsRepository> _settingsRepo = new();
    private readonly Mock<INotificationRecipientRepository> _recipientsRepo = new();
    private readonly Mock<ISmtpSender> _sender = new();
    private readonly Mock<IDataProtectionService> _dp = new();
    private readonly NotificationDispatcher _sut;

    public NotificationDispatcherTests()
    {
        _uow.Setup(u => u.NotificationSettings).Returns(_settingsRepo.Object);
        _uow.Setup(u => u.NotificationRecipients).Returns(_recipientsRepo.Object);
        _dp.Setup(p => p.Unprotect(It.IsAny<string>())).Returns<string>(s => s);

        _sut = new NotificationDispatcher(_uow.Object, _sender.Object, _dp.Object,
            NullLogger<NotificationDispatcher>.Instance);
    }

    [Fact]
    public async Task EnrollmentRejected_DoesNothing_WhenSettingsDisabled()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings { Enabled = false, SmtpHost = "x", NotifyOnEnrollmentRejected = true });

        await _sut.NotifyEnrollmentRejectedAsync(BuildContext());

        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnrollmentRejected_DoesNothing_WhenEventToggleOff()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings
            {
                Enabled = true,
                SmtpHost = "smtp",
                FromAddress = "k@x",
                NotifyOnEnrollmentRejected = false
            });

        await _sut.NotifyEnrollmentRejectedAsync(BuildContext());

        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnrollmentRejected_SendsToSubscribedRecipientsOnly()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings
            {
                Enabled = true,
                SmtpHost = "smtp.example.org",
                SmtpPort = 587,
                TlsMode = SmtpTlsMode.StartTls,
                AuthMode = SmtpAuthMode.None,
                FromAddress = "k@example.org",
                NotifyOnEnrollmentRejected = true
            });

        _recipientsRepo.Setup(r => r.GetSubscribedToAsync(NotificationEventType.EnrollmentRejected, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new NotificationRecipient { Email = "sec@example.org", NotifyOnEnrollmentRejected = true }
            });

        NotificationMessage? captured = null;
        _sender.Setup(s => s.SendAsync(It.IsAny<SmtpDispatchConfig>(), It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SmtpDispatchConfig, NotificationMessage, CancellationToken>((_, m, _) => captured = m)
            .Returns(Task.CompletedTask);

        await _sut.NotifyEnrollmentRejectedAsync(BuildContext());

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(NotificationEventType.EnrollmentRejected);
        captured.Recipients.Should().ContainSingle(r => r.Email == "sec@example.org");
        captured.Body.Should().Contain("Unknown device");
    }

    [Fact]
    public async Task EnrollmentRejected_SwallowsExceptionsFromSender()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings
            {
                Enabled = true,
                SmtpHost = "smtp.example.org",
                FromAddress = "k@example.org",
                NotifyOnEnrollmentRejected = true
            });

        _recipientsRepo.Setup(r => r.GetSubscribedToAsync(NotificationEventType.EnrollmentRejected, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new NotificationRecipient { Email = "ops@example.org", NotifyOnEnrollmentRejected = true } });

        _sender.Setup(s => s.SendAsync(It.IsAny<SmtpDispatchConfig>(), It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP host unreachable"));

        Func<Task> act = () => _sut.NotifyEnrollmentRejectedAsync(BuildContext());

        await act.Should().NotThrowAsync(
            "notification failures must never break the enrollment hot path");
    }

    [Fact]
    public async Task ExpiryNotice_RoutedToExpirySubscribersOnly()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings
            {
                Enabled = true,
                SmtpHost = "smtp.example.org",
                FromAddress = "k@example.org",
                NotifyOnCertificateNearExpiry = true
            });

        _recipientsRepo.Setup(r => r.GetSubscribedToAsync(NotificationEventType.CertificateNearExpiry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new NotificationRecipient { Email = "ops@example.org", NotifyOnCertificateNearExpiry = true } });

        NotificationMessage? captured = null;
        _sender.Setup(s => s.SendAsync(It.IsAny<SmtpDispatchConfig>(), It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SmtpDispatchConfig, NotificationMessage, CancellationToken>((_, m, _) => captured = m)
            .Returns(Task.CompletedTask);

        await _sut.NotifyCertificateNearExpiryAsync(new CertificateExpiryContext(
            SerialNumber: "ABCDEF",
            SubjectDn: "CN=imager-1",
            DeviceId: "imager-1",
            NotAfterUtc: DateTime.UtcNow.AddDays(5),
            DaysUntilExpiry: 5));

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(NotificationEventType.CertificateNearExpiry);
        captured.Subject.Should().Contain("5 day(s)");
    }

    private static EnrollmentRejectionContext BuildContext() => new(
        Operation: "Enrollment",
        SubjectDn: "CN=imager-1",
        DeviceId: "imager-1",
        ClientIp: "10.0.0.5",
        Reason: "Unknown device subject common name",
        OccurredAtUtc: DateTime.UtcNow);
}
