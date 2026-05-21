using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Application.Services;

public interface INotificationSettingsService
{
    Task<NotificationSettingsDto> GetAsync(CancellationToken ct = default);
    Task<NotificationSettingsDto> UpdateAsync(NotificationSettingsUpdateDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationRecipientDto>> ListRecipientsAsync(CancellationToken ct = default);
    Task<NotificationRecipientDto> UpsertRecipientAsync(NotificationRecipientUpsertDto dto, CancellationToken ct = default);
    Task<bool> DeleteRecipientAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Sends a single test email using the supplied draft settings + recipient. Exceptions
    /// propagate so the controller can return them to the UI.
    /// </summary>
    Task SendTestEmailAsync(NotificationTestRequestDto request, CancellationToken ct = default);
}
