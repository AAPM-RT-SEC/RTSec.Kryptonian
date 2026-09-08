using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
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
    private IDeviceRepository? _devices;
    private ICertificateRepository? _certificates;
    private IEnrollmentEventRepository? _enrollmentEvents;
    private IAcmeAccountRepository? _acmeAccounts;
    private IGatewaySettingsRepository? _gatewaySettings;
    private INotificationSettingsRepository? _notificationSettings;
    private INotificationRecipientRepository? _notificationRecipients;
    private IUserRepository? _users;
    private IApiKeyRepository? _apiKeys;

    public UnitOfWork(KryptonianDbContext context)
    {
        _context = context;
    }

    public ICaBackendRepository CaBackends =>
        _caBackends ??= new CaBackendRepository(_context);

    public IEstProfileRepository EstProfiles =>
        _estProfiles ??= new EstProfileRepository(_context);

    public IDeviceRepository Devices =>
        _devices ??= new DeviceRepository(_context);

    public ICertificateRepository Certificates =>
        _certificates ??= new CertificateRepository(_context);

    public IEnrollmentEventRepository EnrollmentEvents =>
        _enrollmentEvents ??= new EnrollmentEventRepository(_context);

    public IAcmeAccountRepository AcmeAccounts =>
        _acmeAccounts ??= new AcmeAccountRepository(_context);

    public IGatewaySettingsRepository GatewaySettings =>
        _gatewaySettings ??= new GatewaySettingsRepository(_context);

    public INotificationSettingsRepository NotificationSettings =>
        _notificationSettings ??= new NotificationSettingsRepository(_context);

    public INotificationRecipientRepository NotificationRecipients =>
        _notificationRecipients ??= new NotificationRecipientRepository(_context);

    public IUserRepository Users =>
        _users ??= new UserRepository(_context);

    public IApiKeyRepository ApiKeys =>
        _apiKeys ??= new ApiKeyRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> TryConsumeActivationCodeAsync(Device device, CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

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
