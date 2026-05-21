using FluentAssertions;
using Moq;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Notifications;
using RTSec.Kryptonian.Application.Services;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Tests.Services;

public class NotificationSettingsServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<INotificationSettingsRepository> _settingsRepo = new();
    private readonly Mock<INotificationRecipientRepository> _recipientsRepo = new();
    private readonly Mock<IDataProtectionService> _dp = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly NotificationSettingsService _sut;

    public NotificationSettingsServiceTests()
    {
        _uow.Setup(u => u.NotificationSettings).Returns(_settingsRepo.Object);
        _uow.Setup(u => u.NotificationRecipients).Returns(_recipientsRepo.Object);
        _dp.Setup(p => p.Protect(It.IsAny<string>())).Returns<string>(s => $"ENC({s})");
        _dp.Setup(p => p.Unprotect(It.IsAny<string>())).Returns<string>(s =>
            s.StartsWith("ENC(", StringComparison.Ordinal) ? s[4..^1] : s);

        _sut = new NotificationSettingsService(_uow.Object, _dp.Object, _dispatcher.Object);
    }

    [Fact]
    public async Task GetAsyncCreatesSingletonWhenMissing()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationSettings?)null);

        var dto = await _sut.GetAsync();

        dto.Should().NotBeNull();
        dto.HasPassword.Should().BeFalse();
        _settingsRepo.Verify(r => r.Add(It.IsAny<NotificationSettings>()), Times.Once);
    }

    [Fact]
    public async Task GetAsyncNeverExposesPlainPassword()
    {
        var settings = ValidStoredSettings(encryptedPassword: "ENC(secret)");
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var dto = await _sut.GetAsync();

        dto.HasPassword.Should().BeTrue();
        // The DTO has no password field; verifying via serialization-irrelevant property check
        typeof(NotificationSettingsDto).GetProperty("Password").Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsyncPreservesExistingPasswordWhenDtoPasswordIsNull()
    {
        var settings = ValidStoredSettings(encryptedPassword: "ENC(existing)");
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var dto = ValidUpdateDto();
        dto.Password = null;

        await _sut.UpdateAsync(dto);

        settings.EncryptedPassword.Should().Be("ENC(existing)");
    }

    [Fact]
    public async Task UpdateAsyncClearsPasswordWhenDtoPasswordIsEmpty()
    {
        var settings = ValidStoredSettings(encryptedPassword: "ENC(existing)");
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var dto = ValidUpdateDto();
        dto.Password = "";

        await _sut.UpdateAsync(dto);

        settings.EncryptedPassword.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsyncEncryptsNewPassword()
    {
        var settings = ValidStoredSettings();
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var dto = ValidUpdateDto();
        dto.Password = "supersecret";

        await _sut.UpdateAsync(dto);

        settings.EncryptedPassword.Should().Be("ENC(supersecret)");
    }

    [Fact]
    public async Task UpdateAsyncRejectsEmptyHost()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidStoredSettings());

        var dto = ValidUpdateDto();
        dto.SmtpHost = " ";

        Func<Task> act = () => _sut.UpdateAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*SMTP host*");
    }

    [Fact]
    public async Task UpdateAsyncRejectsAuthModeWithoutUsername()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidStoredSettings());

        var dto = ValidUpdateDto();
        dto.AuthMode = "basic";
        dto.Username = null;

        Func<Task> act = () => _sut.UpdateAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Username*");
    }

    [Fact]
    public async Task UpdateAsyncRejectsInvalidEmail()
    {
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidStoredSettings());

        var dto = ValidUpdateDto();
        dto.FromAddress = "not-an-email";

        Func<Task> act = () => _sut.UpdateAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertRecipientNormalizesDuplicateEmails()
    {
        var existing = new NotificationRecipient
        {
            Id = Guid.NewGuid(),
            Email = "ops@example.org",
            NotifyOnCertificateNearExpiry = false
        };
        _recipientsRepo.Setup(r => r.GetByEmailAsync("OPS@example.org", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new NotificationRecipientUpsertDto
        {
            Email = "OPS@example.org",
            NotifyOnCertificateNearExpiry = true
        };

        var result = await _sut.UpsertRecipientAsync(dto);

        result.Email.Should().Be("ops@example.org");
        existing.NotifyOnCertificateNearExpiry.Should().BeTrue();
        _recipientsRepo.Verify(r => r.Add(It.IsAny<NotificationRecipient>()), Times.Never);
    }

    [Fact]
    public async Task SendTestEmailAsyncUsesStoredPasswordWhenDtoPasswordOmitted()
    {
        var stored = ValidStoredSettings(encryptedPassword: "ENC(realpw)");
        _settingsRepo.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        SmtpDispatchConfig? captured = null;
        _dispatcher.Setup(d => d.SendTestEmailAsync(
                It.IsAny<SmtpDispatchConfig>(),
                It.IsAny<NotificationAddress>(),
                It.IsAny<CancellationToken>()))
            .Callback<SmtpDispatchConfig, NotificationAddress, CancellationToken>((c, _, _) => captured = c)
            .Returns(Task.CompletedTask);

        var request = new NotificationTestRequestDto
        {
            Settings = ValidUpdateDto(),
            RecipientEmail = "admin@example.org"
        };
        request.Settings.Password = null;

        await _sut.SendTestEmailAsync(request);

        captured.Should().NotBeNull();
        captured!.Password.Should().Be("realpw");
    }

    private static NotificationSettings ValidStoredSettings(string? encryptedPassword = null) => new()
    {
        Id = Guid.NewGuid(),
        Enabled = true,
        SmtpHost = "smtp.example.org",
        SmtpPort = 587,
        TlsMode = SmtpTlsMode.StartTls,
        AuthMode = SmtpAuthMode.None,
        FromAddress = "kryptonian@example.org",
        ExpiryWarningDays = 14,
        EncryptedPassword = encryptedPassword
    };

    private static NotificationSettingsUpdateDto ValidUpdateDto() => new()
    {
        Enabled = true,
        SmtpHost = "smtp.example.org",
        SmtpPort = 587,
        TlsMode = "starttls",
        AuthMode = "none",
        FromAddress = "kryptonian@example.org",
        NotifyOnEnrollmentRejected = true,
        NotifyOnCertificateNearExpiry = true,
        ExpiryWarningDays = 14
    };
}
