using System.Data;
using Microsoft.EntityFrameworkCore;

namespace RTSec.Kryptonian.Infrastructure.Data;

public static class SchemaUpgrades
{
    // Existing installations use EnsureCreated, not EF migrations. Keep this
    // additive: never recreate a populated database to add a policy setting.
    public static async Task EnsureTrustPolicyAsync(KryptonianDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE est_profiles ADD COLUMN IF NOT EXISTS validate_client_certificate_chain boolean NOT NULL DEFAULT false;
                ALTER TABLE est_profiles ADD COLUMN IF NOT EXISTS trusted_client_ca_thumbprints text[] NOT NULL DEFAULT '{}';
                """, ct);
            return;
        }
        if (!db.Database.IsSqlite()) return;

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(est_profiles)";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(1));

            if (!columns.Contains("validate_client_certificate_chain"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE est_profiles ADD COLUMN validate_client_certificate_chain INTEGER NOT NULL DEFAULT 0", ct);
            if (!columns.Contains("trusted_client_ca_thumbprints"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE est_profiles ADD COLUMN trusted_client_ca_thumbprints TEXT NOT NULL DEFAULT '[]'", ct);
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }
}
