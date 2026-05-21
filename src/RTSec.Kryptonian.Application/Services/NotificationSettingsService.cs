using System.Net.Mail;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Notifications;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

public class NotificationSettingsService : INotificationSettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataProtectionService _dataProtection;
    private readonly INotificationDispatcher _dispatcher;

    public NotificationSettingsService(
        IUnitOfWork unitOfWork,
        IDataProtectionService dataProtection,
        INotificationDispatcher dispatcher)
    {
        _unitOfWork = unitOfWork;
        _dataProtection = dataProtection;
        _dispatcher = dispatcher;
    }

    public async Task<NotificationSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await GetOrCreateAsync(ct);
        return ToDto(settings);
    }

    public async Task<NotificationSettingsDto> UpdateAsync(NotificationSettingsUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Validate(dto);

        var settings = await GetOrCreateAsync(ct);
        ApplyUpdate(settings, dto);
        _unitOfWork.NotificationSettings.Update(settings);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task<IReadOnlyList<NotificationRecipientDto>> ListRecipientsAsync(CancellationToken ct = default)
    {
        var recipients = await _unitOfWork.NotificationRecipients.GetAllAsync(ct);
        return recipients
            .OrderBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    public async Task<NotificationRecipientDto> UpsertRecipientAsync(NotificationRecipientUpsertDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateEmail(dto.Email);

        var existing = await _unitOfWork.NotificationRecipients.GetByEmailAsync(dto.Email, ct);
        if (existing == null)
        {
            var recipient = new NotificationRecipient
            {
                Email = dto.Email.Trim(),
                DisplayName = NormalizeOptional(dto.DisplayName),
                NotifyOnEnrollmentRejected = dto.NotifyOnEnrollmentRejected,
                NotifyOnCertificateNearExpiry = dto.NotifyOnCertificateNearExpiry
            };
            _unitOfWork.NotificationRecipients.Add(recipient);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(recipient);
        }

        existing.DisplayName = NormalizeOptional(dto.DisplayName);
        existing.NotifyOnEnrollmentRejected = dto.NotifyOnEnrollmentRejected;
        existing.NotifyOnCertificateNearExpiry = dto.NotifyOnCertificateNearExpiry;
        _unitOfWork.NotificationRecipients.Update(existing);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    public async Task<bool> DeleteRecipientAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _unitOfWork.NotificationRecipients.GetByIdAsync(id, ct);
        if (existing == null)
        {
            return false;
        }

        _unitOfWork.NotificationRecipients.Delete(existing);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task SendTestEmailAsync(NotificationTestRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEmail(request.RecipientEmail);
        Validate(request.Settings);

        var stored = await _unitOfWork.NotificationSettings.GetSingletonAsync(ct);
        string? password;
        if (request.Settings.Password != null)
        {
            password = request.Settings.Password;
        }
        else if (!string.IsNullOrEmpty(stored?.EncryptedPassword))
        {
            password = _dataProtection.Unprotect(stored.EncryptedPassword);
        }
        else
        {
            password = null;
        }

        var config = new SmtpDispatchConfig(
            Host: request.Settings.SmtpHost.Trim(),
            Port: request.Settings.SmtpPort,
            TlsMode: ParseTls(request.Settings.TlsMode),
            AuthMode: ParseAuth(request.Settings.AuthMode),
            Username: NormalizeOptional(request.Settings.Username),
            Password: password,
            FromAddress: request.Settings.FromAddress.Trim(),
            FromDisplayName: NormalizeOptional(request.Settings.FromDisplayName),
            TrustServerCertificate: request.Settings.TrustServerCertificate);

        await _dispatcher.SendTestEmailAsync(
            config,
            new NotificationAddress(request.RecipientEmail.Trim()),
            ct);
    }

    private void ApplyUpdate(NotificationSettings settings, NotificationSettingsUpdateDto dto)
    {
        settings.Enabled = dto.Enabled;
        settings.SmtpHost = dto.SmtpHost.Trim();
        settings.SmtpPort = dto.SmtpPort;
        settings.TlsMode = ParseTls(dto.TlsMode);
        settings.AuthMode = ParseAuth(dto.AuthMode);
        settings.Username = NormalizeOptional(dto.Username);
        settings.FromAddress = dto.FromAddress.Trim();
        settings.FromDisplayName = NormalizeOptional(dto.FromDisplayName);
        settings.TrustServerCertificate = dto.TrustServerCertificate;
        settings.NotifyOnEnrollmentRejected = dto.NotifyOnEnrollmentRejected;
        settings.NotifyOnCertificateNearExpiry = dto.NotifyOnCertificateNearExpiry;
        settings.ExpiryWarningDays = dto.ExpiryWarningDays;

        // Password update semantics: null = leave alone, empty = clear, otherwise encrypt new value.
        if (dto.Password != null)
        {
            settings.EncryptedPassword = string.IsNullOrEmpty(dto.Password)
                ? null
                : _dataProtection.Protect(dto.Password);
        }
    }

    private async Task<NotificationSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var existing = await _unitOfWork.NotificationSettings.GetSingletonAsync(ct);
        if (existing != null) return existing;

        var fresh = new NotificationSettings { Id = Guid.NewGuid() };
        _unitOfWork.NotificationSettings.Add(fresh);
        await _unitOfWork.SaveChangesAsync(ct);
        return fresh;
    }

    private static void Validate(NotificationSettingsUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SmtpHost))
            throw new ArgumentException("SMTP host is required.");
        if (dto.SmtpPort is <= 0 or > 65535)
            throw new ArgumentException("SMTP port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(dto.FromAddress))
            throw new ArgumentException("From address is required.");
        ValidateEmail(dto.FromAddress);
        if (dto.ExpiryWarningDays < NotificationSettings.MinExpiryWarningDays
            || dto.ExpiryWarningDays > NotificationSettings.MaxExpiryWarningDays)
        {
            throw new ArgumentException(
                $"Expiry warning days must be between {NotificationSettings.MinExpiryWarningDays} and {NotificationSettings.MaxExpiryWarningDays}.");
        }

        _ = ParseTls(dto.TlsMode);
        var auth = ParseAuth(dto.AuthMode);
        if (auth != SmtpAuthMode.None && string.IsNullOrWhiteSpace(dto.Username))
        {
            throw new ArgumentException("Username is required when auth mode is not 'none'.");
        }
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.");
        try
        {
            _ = new MailAddress(email.Trim());
        }
        catch (FormatException)
        {
            throw new ArgumentException($"'{email}' is not a valid email address.");
        }
    }

    private static SmtpTlsMode ParseTls(string value) => value?.ToLowerInvariant() switch
    {
        "none" => SmtpTlsMode.None,
        "starttls" or null or "" => SmtpTlsMode.StartTls,
        "implicit" => SmtpTlsMode.Implicit,
        _ => throw new ArgumentException($"Unknown TLS mode '{value}'. Valid: none, starttls, implicit.")
    };

    private static SmtpAuthMode ParseAuth(string value) => value?.ToLowerInvariant() switch
    {
        "none" or null or "" => SmtpAuthMode.None,
        "basic" => SmtpAuthMode.Basic,
        "ntlm" => SmtpAuthMode.Ntlm,
        _ => throw new ArgumentException($"Unknown auth mode '{value}'. Valid: none, basic, ntlm.")
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private NotificationSettingsDto ToDto(NotificationSettings entity) => new()
    {
        Id = entity.Id.ToString(),
        Enabled = entity.Enabled,
        SmtpHost = entity.SmtpHost,
        SmtpPort = entity.SmtpPort,
        TlsMode = entity.TlsMode.ToString().ToLowerInvariant(),
        AuthMode = entity.AuthMode.ToString().ToLowerInvariant(),
        Username = entity.Username,
        HasPassword = !string.IsNullOrEmpty(entity.EncryptedPassword),
        FromAddress = entity.FromAddress,
        FromDisplayName = entity.FromDisplayName,
        TrustServerCertificate = entity.TrustServerCertificate,
        NotifyOnEnrollmentRejected = entity.NotifyOnEnrollmentRejected,
        NotifyOnCertificateNearExpiry = entity.NotifyOnCertificateNearExpiry,
        ExpiryWarningDays = entity.ExpiryWarningDays,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static NotificationRecipientDto ToDto(NotificationRecipient entity) => new()
    {
        Id = entity.Id.ToString(),
        Email = entity.Email,
        DisplayName = entity.DisplayName,
        NotifyOnEnrollmentRejected = entity.NotifyOnEnrollmentRejected,
        NotifyOnCertificateNearExpiry = entity.NotifyOnCertificateNearExpiry,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
