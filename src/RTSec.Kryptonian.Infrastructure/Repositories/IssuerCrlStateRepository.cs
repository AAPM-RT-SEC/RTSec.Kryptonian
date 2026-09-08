using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class IssuerCrlStateRepository(KryptonianDbContext context)
    : BaseRepository<IssuerCrlState>(context), IIssuerCrlStateRepository
{
    public Task<IssuerCrlState?> GetByIssuerFingerprintAsync(string issuerFingerprint, CancellationToken ct = default) =>
        _dbSet.SingleOrDefaultAsync(state => state.IssuerFingerprint == issuerFingerprint, ct);

    public Task<IssuerCrlState?> GetByCaBackendIdAsync(Guid caBackendId, CancellationToken ct = default) =>
        _dbSet.SingleOrDefaultAsync(state => state.CaBackendId == caBackendId, ct);
}
