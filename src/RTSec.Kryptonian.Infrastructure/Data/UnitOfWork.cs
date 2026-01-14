using Microsoft.EntityFrameworkCore.Storage;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Repositories;

namespace RTSec.Kryptonian.Infrastructure.Data;

/// <summary>
/// Unit of Work implementation for coordinating changes across multiple repositories.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly KryptonianDbContext _context;
    private IDbContextTransaction? _transaction;

    private ICaBackendRepository? _caBackends;
    private IEstProfileRepository? _estProfiles;
    private ICertificateRepository? _certificates;
    private IEnrollmentEventRepository? _enrollmentEvents;
    private IAcmeAccountRepository? _acmeAccounts;

    public UnitOfWork(KryptonianDbContext context)
    {
        _context = context;
    }

    public ICaBackendRepository CaBackends =>
        _caBackends ??= new CaBackendRepository(_context);

    public IEstProfileRepository EstProfiles =>
        _estProfiles ??= new EstProfileRepository(_context);

    public ICertificateRepository Certificates =>
        _certificates ??= new CertificateRepository(_context);

    public IEnrollmentEventRepository EnrollmentEvents =>
        _enrollmentEvents ??= new EnrollmentEventRepository(_context);

    public IAcmeAccountRepository AcmeAccounts =>
        _acmeAccounts ??= new AcmeAccountRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
