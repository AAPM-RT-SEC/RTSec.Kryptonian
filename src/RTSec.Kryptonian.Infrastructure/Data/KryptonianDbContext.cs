using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data;

public class KryptonianDbContext : DbContext
{
    public KryptonianDbContext(DbContextOptions<KryptonianDbContext> options)
        : base(options)
    {
    }

    public DbSet<CaBackend> CaBackends => Set<CaBackend>();
    public DbSet<EstProfile> EstProfiles => Set<EstProfile>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<EnrollmentEvent> EnrollmentEvents => Set<EnrollmentEvent>();
    public DbSet<AcmeAccount> AcmeAccounts => Set<AcmeAccount>();
    public DbSet<GatewaySettings> GatewaySettings => Set<GatewaySettings>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KryptonianDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
