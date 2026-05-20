using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.Services;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class EstProfileRepository : BaseRepository<EstProfile>, IEstProfileRepository
{
    public EstProfileRepository(KryptonianDbContext context) : base(context)
    {
    }

    public override async Task<EstProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(p => p.CaBackend)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<EstProfile?> GetByPathAndHostnameAsync(string pathPrefix, string hostname, CancellationToken ct = default)
    {
        var normalizedHostname = HostnameMatcher.NormalizeHostname(hostname);

        // First, try exact matches (most common case).
        // Hostname lists are value-converted collections, so keep the comparison explicit
        // and case-insensitive instead of relying on provider-specific collection Contains.
        var exactCandidates = await _dbSet
            .Include(p => p.CaBackend)
            .Where(p => p.IsEnabled)
            .Where(p => p.PathPrefix == pathPrefix)
            .Where(p => p.HostnameMatchType == HostnameMatchType.Exact)
            .ToListAsync(ct);

        var exactMatch = exactCandidates.FirstOrDefault(p => HostnameMatcher.Matches(p, normalizedHostname));

        if (exactMatch != null)
            return exactMatch;

        // For suffix and wildcard matches, we need to load candidates and filter in-memory
        // This is necessary because suffix/wildcard matching can't be expressed efficiently in SQL
        // We optimize by only loading profiles with non-Exact match types for the given path
        var candidates = await _dbSet
            .Include(p => p.CaBackend)
            .Where(p => p.IsEnabled)
            .Where(p => p.PathPrefix == pathPrefix)
            .Where(p => p.HostnameMatchType != HostnameMatchType.Exact)
            .ToListAsync(ct);

        // Find the first matching profile using domain logic
        return candidates.FirstOrDefault(p => HostnameMatcher.Matches(p, normalizedHostname));
    }

    public async Task<IEnumerable<EstProfile>> GetEnabledAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .Include(p => p.CaBackend)
            .Where(p => p.IsEnabled)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<EstProfile>> GetByCaBackendIdAsync(Guid caBackendId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(p => p.CaBackendId == caBackendId)
            .ToListAsync(ct);
    }
}
