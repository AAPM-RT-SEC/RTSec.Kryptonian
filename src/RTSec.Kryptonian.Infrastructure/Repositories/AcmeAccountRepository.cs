using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

/// <summary>
/// Repository for ACME accounts.
/// </summary>
public class AcmeAccountRepository : BaseRepository<AcmeAccount>, IAcmeAccountRepository
{
    public AcmeAccountRepository(KryptonianDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<AcmeAccount?> GetByDirectoryUrlAsync(Uri directoryUrl, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(a => a.DirectoryUrl == directoryUrl && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<AcmeAccount?> GetByDirectoryAndEmailAsync(Uri directoryUrl, string email, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(a => a.DirectoryUrl == directoryUrl && a.Email == email && a.IsActive)
            .FirstOrDefaultAsync(ct);
    }
}
