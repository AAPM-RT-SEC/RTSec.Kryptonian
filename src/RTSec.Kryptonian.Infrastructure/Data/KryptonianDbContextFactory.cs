using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RTSec.Kryptonian.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating DbContext for migrations.
/// </summary>
public class KryptonianDbContextFactory : IDesignTimeDbContextFactory<KryptonianDbContext>
{
    public KryptonianDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KryptonianDbContext>();

        // Use a placeholder connection string for migrations
        // Actual connection string is provided at runtime via environment variables
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=kryptonian;Username=postgres;Password=postgres",
            options => options.MigrationsAssembly("RTSec.Kryptonian.Infrastructure"));

        return new KryptonianDbContext(optionsBuilder.Options);
    }
}
