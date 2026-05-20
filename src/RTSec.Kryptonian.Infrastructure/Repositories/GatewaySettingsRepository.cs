using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class GatewaySettingsRepository : BaseRepository<GatewaySettings>, IGatewaySettingsRepository
{
    public GatewaySettingsRepository(KryptonianDbContext context)
        : base(context)
    {
    }

    public async Task<GatewaySettings?> GetSingletonAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .OrderBy(settings => settings.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
