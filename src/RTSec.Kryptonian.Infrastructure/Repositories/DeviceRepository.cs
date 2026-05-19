using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class DeviceRepository : BaseRepository<Device>, IDeviceRepository
{
    public DeviceRepository(KryptonianDbContext context) : base(context)
    {
    }

    public async Task<Device?> GetBySubjectCommonNameAsync(string subjectCommonName, CancellationToken ct = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(d => d.SubjectCommonName == subjectCommonName, ct);
    }

    public override async Task<IEnumerable<Device>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .OrderBy(d => d.DisplayName)
            .ToListAsync(ct);
    }
}
