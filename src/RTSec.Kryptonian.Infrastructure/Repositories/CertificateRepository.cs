using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class CertificateRepository : BaseRepository<Certificate>, ICertificateRepository
{
    public CertificateRepository(KryptonianDbContext context) : base(context)
    {
    }

    public async Task<Certificate?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.SerialNumber == serialNumber, ct);
    }

    public async Task<Certificate?> GetByThumbprintAsync(string thumbprint, CancellationToken ct = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint, ct);
    }

    public async Task<IEnumerable<Certificate>> GetByEstProfileIdAsync(Guid estProfileId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(c => c.EstProfileId == estProfileId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Certificate>> GetExpiringAsync(DateTime before, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(c => c.Status == CertificateStatus.Valid)
            .Where(c => c.NotAfter <= before)
            .OrderBy(c => c.NotAfter)
            .ToListAsync(ct);
    }
}
