using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Tests.Data;

public class SchemaUpgradesTests
{
    [Fact]
    public async Task AddsTrustSettingsToExistingSqliteWithoutReplacingRowsAndCanRunAgain()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new KryptonianDbContext(new DbContextOptionsBuilder<KryptonianDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE est_profiles (id TEXT PRIMARY KEY, name TEXT NOT NULL); INSERT INTO est_profiles VALUES ('existing', 'Keep this profile');");

        await SchemaUpgrades.EnsureTrustPolicyAsync(db);
        await SchemaUpgrades.EnsureTrustPolicyAsync(db);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, validate_client_certificate_chain, trusted_client_ca_thumbprints FROM est_profiles WHERE id = 'existing'";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Keep this profile", reader.GetString(0));
        Assert.False(reader.GetBoolean(1));
        Assert.Equal("[]", reader.GetString(2));
        Assert.False(await reader.ReadAsync());
    }
}
