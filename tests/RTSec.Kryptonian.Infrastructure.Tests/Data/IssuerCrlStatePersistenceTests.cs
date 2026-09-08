using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Infrastructure.Data;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Data;

public class IssuerCrlStatePersistenceTests
{
    [Fact]
    public async Task RevocationAndIssuerCrlStatePersistAcrossSqliteContexts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KryptonianDbContext>().UseSqlite(connection).Options;
        var certificateId = Guid.NewGuid();
        var backendId = Guid.NewGuid();

        await using (var write = new KryptonianDbContext(options))
        {
            await write.Database.EnsureCreatedAsync();
            var profileId = Guid.NewGuid();
            write.CaBackends.Add(new CaBackend { Id = backendId, Name = "local", Type = CaBackendType.SelfSigned });
            write.EstProfiles.Add(new EstProfile { Id = profileId, Name = "default", CaBackendId = backendId });
            write.Certificates.Add(new Certificate
            {
                Id = certificateId, SerialNumber = "01", SubjectDn = "CN=device", IssuerDn = "CN=CA", Thumbprint = "thumb",
                CertificatePem = "pem", EstProfileId = profileId, CaBackendId = backendId,
                Status = CertificateStatus.Revoked, RevokedAt = DateTime.UtcNow, RevocationReason = RevocationReason.KeyCompromise
            });
            write.IssuerCrlStates.Add(new IssuerCrlState
            {
                Id = Guid.NewGuid(), CaBackendId = backendId, IssuerFingerprint = "ABCD", CrlNumber = 2,
                ThisUpdate = DateTime.UtcNow, NextUpdate = DateTime.UtcNow.AddHours(1), CrlDerBase64 = "AQID"
            });
            await write.SaveChangesAsync();
        }

        await using var read = new KryptonianDbContext(options);
        var certificate = await read.Certificates.SingleAsync(c => c.Id == certificateId);
        var crl = await read.IssuerCrlStates.SingleAsync();
        Assert.Equal(CertificateStatus.Revoked, certificate.Status);
        Assert.Equal(RevocationReason.KeyCompromise, certificate.RevocationReason);
        Assert.Equal(2, crl.CrlNumber);
        Assert.Equal("AQID", crl.CrlDerBase64);
    }

    [Fact]
    public async Task CrlNumberRejectsConcurrentUpdates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KryptonianDbContext>().UseSqlite(connection).Options;
        var stateId = Guid.NewGuid();
        await using (var seed = new KryptonianDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.IssuerCrlStates.Add(new IssuerCrlState
            {
                Id = stateId, CaBackendId = Guid.NewGuid(), IssuerFingerprint = "ABCD", CrlNumber = 1,
                ThisUpdate = DateTime.UtcNow, NextUpdate = DateTime.UtcNow.AddHours(1), CrlDerBase64 = "AQID"
            });
            await seed.SaveChangesAsync();
        }

        await using var first = new KryptonianDbContext(options);
        await using var second = new KryptonianDbContext(options);
        var firstState = await first.IssuerCrlStates.SingleAsync(s => s.Id == stateId);
        var secondState = await second.IssuerCrlStates.SingleAsync(s => s.Id == stateId);
        firstState.CrlNumber++;
        await first.SaveChangesAsync();
        secondState.CrlNumber++;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
