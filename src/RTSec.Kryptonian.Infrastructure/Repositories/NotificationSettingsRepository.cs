using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class NotificationSettingsRepository : BaseRepository<NotificationSettings>, INotificationSettingsRepository
{
    public NotificationSettingsRepository(KryptonianDbContext context)
        : base(context)
    {
    }

    public async Task<NotificationSettings?> GetSingletonAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
