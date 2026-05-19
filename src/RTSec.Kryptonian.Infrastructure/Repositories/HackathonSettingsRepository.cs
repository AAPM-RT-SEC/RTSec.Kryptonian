using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class HackathonSettingsRepository : BaseRepository<HackathonSettings>, IHackathonSettingsRepository
{
    public HackathonSettingsRepository(KryptonianDbContext context)
        : base(context)
    {
    }

    public async Task<HackathonSettings?> GetSingletonAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .OrderBy(settings => settings.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
