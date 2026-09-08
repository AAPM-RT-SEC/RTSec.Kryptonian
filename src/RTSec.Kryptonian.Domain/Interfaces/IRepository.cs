using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Domain.Interfaces;

/// <summary>
/// Unit of Work interface for coordinating changes across multiple repositories.
/// Allows atomic multi-entity operations and explicit transaction control.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    ICaBackendRepository CaBackends { get; }
    IEstProfileRepository EstProfiles { get; }
    IDeviceRepository Devices { get; }
    ICertificateRepository Certificates { get; }
    IEnrollmentEventRepository EnrollmentEvents { get; }
    IAcmeAccountRepository AcmeAccounts { get; }
    IGatewaySettingsRepository GatewaySettings { get; }
    INotificationSettingsRepository NotificationSettings { get; }
    INotificationRecipientRepository NotificationRecipients { get; }
    IUserRepository Users { get; }
    IApiKeyRepository ApiKeys { get; }

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists a one-time activation-code consumption, returning false when a concurrent
    /// request already consumed the same code.
    /// </summary>
    Task<bool> TryConsumeActivationCodeAsync(Device device, CancellationToken ct = default);

    /// <summary>
    /// Begins a database transaction for explicit transaction control.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

/// <summary>
/// Generic repository interface.
/// Repositories no longer auto-save; use IUnitOfWork.SaveChangesAsync() to persist changes.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
}

/// <summary>
/// Repository for CA backends.
/// </summary>
public interface ICaBackendRepository : IRepository<CaBackend>
{
    Task<IEnumerable<CaBackend>> GetEnabledAsync(CancellationToken ct = default);
    Task<CaBackend?> GetActiveAsync(CancellationToken ct = default);
}

public interface IDeviceRepository : IRepository<Device>
{
    Task<Device?> GetBySubjectCommonNameAsync(string subjectCommonName, CancellationToken ct = default);
}

/// <summary>
/// Repository for EST profiles.
/// </summary>
public interface IEstProfileRepository : IRepository<EstProfile>
{
    Task<EstProfile?> GetByPathAndHostnameAsync(string pathPrefix, string hostname, CancellationToken ct = default);
    Task<IEnumerable<EstProfile>> GetEnabledAsync(CancellationToken ct = default);
    Task<IEnumerable<EstProfile>> GetByCaBackendIdAsync(Guid caBackendId, CancellationToken ct = default);
}

/// <summary>
/// Repository for certificates.
/// </summary>
public interface ICertificateRepository : IRepository<Certificate>
{
    Task<Certificate?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default);
    Task<Certificate?> GetByThumbprintAsync(string thumbprint, CancellationToken ct = default);
    Task<IEnumerable<Certificate>> GetByEstProfileIdAsync(Guid estProfileId, CancellationToken ct = default);
    Task<IEnumerable<Certificate>> GetByDeviceRecordIdAsync(Guid deviceId, CancellationToken ct = default);
    Task<Certificate?> GetMostRecentBridgeCertificateAsync(CancellationToken ct = default);
    Task<IEnumerable<Certificate>> GetExpiringAsync(DateTime before, CancellationToken ct = default);
}

/// <summary>
/// Repository for enrollment events.
/// </summary>
public interface IEnrollmentEventRepository : IRepository<EnrollmentEvent>
{
    Task<IEnumerable<EnrollmentEvent>> GetByProfileIdAsync(Guid profileId, int limit = 50, CancellationToken ct = default);
    Task<IEnumerable<EnrollmentEvent>> GetRecentAsync(int limit = 50, CancellationToken ct = default);
    Task<IEnumerable<EnrollmentEvent>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct = default);
}

/// <summary>
/// Repository for ACME accounts.
/// </summary>
public interface IAcmeAccountRepository : IRepository<AcmeAccount>
{
    /// <summary>
    /// Gets an active ACME account for the specified directory URL.
    /// </summary>
    Task<AcmeAccount?> GetByDirectoryUrlAsync(Uri directoryUrl, CancellationToken ct = default);

    /// <summary>
    /// Gets an active ACME account for the specified directory URL and email.
    /// </summary>
    Task<AcmeAccount?> GetByDirectoryAndEmailAsync(Uri directoryUrl, string email, CancellationToken ct = default);
}

public interface IGatewaySettingsRepository : IRepository<GatewaySettings>
{
    Task<GatewaySettings?> GetSingletonAsync(CancellationToken ct = default);
}

public interface INotificationSettingsRepository : IRepository<NotificationSettings>
{
    Task<NotificationSettings?> GetSingletonAsync(CancellationToken ct = default);
}

public interface INotificationRecipientRepository : IRepository<NotificationRecipient>
{
    Task<NotificationRecipient?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IEnumerable<NotificationRecipient>> GetSubscribedToAsync(
        Domain.Enums.NotificationEventType eventType,
        CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
}

public interface IApiKeyRepository : IRepository<ApiKey>
{
    Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default);
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ApiKey>> GetAllActiveAsync(CancellationToken ct = default);
}
