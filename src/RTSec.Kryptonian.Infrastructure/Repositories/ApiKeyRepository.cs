using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Repositories;

public class ApiKeyRepository : BaseRepository<ApiKey>, IApiKeyRepository
{
    public ApiKeyRepository(KryptonianDbContext context) : base(context) { }

    public async Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default)
        => await _dbSet
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash
                && k.RevokedAt == null
                && (k.ExpiresAt == null || k.ExpiresAt > DateTimeOffset.UtcNow), ct);

    public async Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _dbSet
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<ApiKey>> GetAllActiveAsync(CancellationToken ct = default)
        => await _dbSet
            .Include(k => k.User)
            .Where(k => k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > DateTimeOffset.UtcNow))
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
}
