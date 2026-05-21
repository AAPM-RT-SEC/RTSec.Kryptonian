using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class NotificationRecipientRepository : BaseRepository<NotificationRecipient>, INotificationRecipientRepository
{
    public NotificationRecipientRepository(KryptonianDbContext context)
        : base(context)
    {
    }

    public async Task<NotificationRecipient?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();
        return await _dbSet
            .FirstOrDefaultAsync(r => r.Email.ToLower() == normalized, ct);
    }

    public async Task<IEnumerable<NotificationRecipient>> GetSubscribedToAsync(
        NotificationEventType eventType,
        CancellationToken ct = default)
    {
        return eventType switch
        {
            NotificationEventType.EnrollmentRejected =>
                await _dbSet.Where(r => r.NotifyOnEnrollmentRejected).ToListAsync(ct),
            NotificationEventType.CertificateNearExpiry =>
                await _dbSet.Where(r => r.NotifyOnCertificateNearExpiry).ToListAsync(ct),
            _ => Array.Empty<NotificationRecipient>()
        };
    }
}
