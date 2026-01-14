using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class EnrollmentEventRepository : BaseRepository<EnrollmentEvent>, IEnrollmentEventRepository
{
    public EnrollmentEventRepository(KryptonianDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EnrollmentEvent>> GetByProfileIdAsync(Guid profileId, int limit = 50, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(e => e.ProfileId == profileId)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<EnrollmentEvent>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
    {
        return await _dbSet
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }
}
