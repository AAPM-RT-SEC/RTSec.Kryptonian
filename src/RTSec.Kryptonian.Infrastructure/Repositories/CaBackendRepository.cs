using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class CaBackendRepository : BaseRepository<CaBackend>, ICaBackendRepository
{
    public CaBackendRepository(KryptonianDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<CaBackend>> GetEnabledAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .Where(c => c.IsEnabled)
            .ToListAsync(ct);
    }
}
