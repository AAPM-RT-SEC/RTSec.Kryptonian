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
                ALTER TABLE certificates ADD COLUMN IF NOT EXISTS revoked_at timestamp with time zone NULL;
                ALTER TABLE certificates ADD COLUMN IF NOT EXISTS revocation_reason text NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_certificates_issuer_serial ON certificates(ca_backend_id, serial_number);
                DROP INDEX IF EXISTS "IX_certificates_serial_number";
                CREATE TABLE IF NOT EXISTS issuer_crl_states (
                    id uuid PRIMARY KEY,
                    ca_backend_id uuid NOT NULL,
                    issuer_fingerprint varchar(128) NOT NULL UNIQUE,
                    crl_number bigint NOT NULL,
                    this_update timestamp with time zone NOT NULL,
                    next_update timestamp with time zone NOT NULL,
                    crl_der_base64 text NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_issuer_crl_states_ca_backend_id ON issuer_crl_states(ca_backend_id);
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

            await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS issuer_crl_states (id TEXT PRIMARY KEY, ca_backend_id TEXT NOT NULL, issuer_fingerprint TEXT NOT NULL UNIQUE, crl_number INTEGER NOT NULL, this_update TEXT NOT NULL, next_update TEXT NOT NULL, crl_der_base64 TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)", ct);
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS ix_issuer_crl_states_ca_backend_id ON issuer_crl_states(ca_backend_id)", ct);

            command.CommandText = "PRAGMA table_info(certificates)";
            columns.Clear();
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(1));
            if (!columns.Contains("revoked_at"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE certificates ADD COLUMN revoked_at TEXT NULL", ct);
            if (!columns.Contains("revocation_reason"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE certificates ADD COLUMN revocation_reason TEXT NULL", ct);
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS ix_certificates_issuer_serial ON certificates(ca_backend_id, serial_number)", ct);
            await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_certificates_serial_number", ct);
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }
}
