using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Tests.Data;

/// <summary>
/// Proves the EST profile trust settings actually reach the database.
///
/// ValidateClientCertificateChain used to be a public field, which EF Core does not map,
/// so an administrator enabling chain validation lost that choice on restart. These tests
/// save in one DbContext and read back in a second over the same connection, which is the
/// only way to observe real persistence rather than change-tracker aliasing.
/// </summary>
public sealed class EstProfilePersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EstProfilePersistenceTests()
    {
        // Opened, in-memory connection kept alive for the whole test so both contexts
        // share one database. Never written to disk, so nothing to clean up.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private KryptonianDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<KryptonianDbContext>()
            .UseSqlite(_connection)
            .Options);

    private static Guid SeedBackend(KryptonianDbContext ctx)
    {
        var backend = new CaBackend
        {
            Id = Guid.NewGuid(),
            Name = "Trust Test CA",
            Type = CaBackendType.SelfSigned,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.CaBackends.Add(backend);
        ctx.SaveChanges();
        return backend.Id;
    }

    [Fact]
    public async Task ValidateFlagAndTrustedThumbprintsSurviveRoundTrip()
    {
        var backendId = Guid.NewGuid();

        using (var ctx1 = CreateContext())
        {
            ctx1.Database.EnsureCreated();
            backendId = SeedBackend(ctx1);

            var profile = new EstProfile
            {
                Id = Guid.NewGuid(),
                Name = "chain-validation-on",
                PathPrefix = "/.well-known/est",
                HostnameMatchType = HostnameMatchType.Exact,
                CaBackendId = backendId,
                ValidityDays = 90,
                RequireClientCertificate = true,
                // The setting that was silently dropped while it was a field.
                ValidateClientCertificateChain = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            profile.Hostnames.Add("est.example.com");
            profile.TrustedClientCaThumbprints.Add("AABBCCDDEEFF00112233445566778899AABBCCDD");
            profile.TrustedClientCaThumbprints.Add("11223344556677889900AABBCCDDEEFF00112233");

            ctx1.EstProfiles.Add(profile);
            await ctx1.SaveChangesAsync();
        }

        // Fresh context: only a real column round-trip can satisfy these.
        await using (var ctx2 = CreateContext())
        {
            var loaded = await ctx2.EstProfiles.SingleAsync(p => p.CaBackendId == backendId);

            loaded.ValidateClientCertificateChain.Should().BeTrue();
            loaded.TrustedClientCaThumbprints.Should().Equal(
                "AABBCCDDEEFF00112233445566778899AABBCCDD",
                "11223344556677889900AABBCCDDEEFF00112233");
        }
    }

    [Fact]
    public async Task ValidateFlagDefaultsToFalseWhenNotChosen()
    {
        Guid backendId;
        using (var ctx1 = CreateContext())
        {
            ctx1.Database.EnsureCreated();
            backendId = SeedBackend(ctx1);

            var profile = new EstProfile
            {
                Id = Guid.NewGuid(),
                Name = "chain-validation-off",
                PathPrefix = "/.well-known/est",
                CaBackendId = backendId,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            profile.Hostnames.Add("localhost");

            ctx1.EstProfiles.Add(profile);
            await ctx1.SaveChangesAsync();
        }

        await using (var ctx2 = CreateContext())
        {
            var loaded = await ctx2.EstProfiles.SingleAsync(p => p.Name == "chain-validation-off");
            loaded.ValidateClientCertificateChain.Should().BeFalse();
            loaded.TrustedClientCaThumbprints.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task InPlaceThumbprintListMutationIsPersisted()
    {
        Guid profileId;
        Guid backendId;

        using (var ctx1 = CreateContext())
        {
            ctx1.Database.EnsureCreated();
            backendId = SeedBackend(ctx1);

            var profile = new EstProfile
            {
                Id = Guid.NewGuid(),
                Name = "thumbprint-mutation",
                PathPrefix = "/.well-known/est",
                CaBackendId = backendId,
                ValidateClientCertificateChain = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            profile.Hostnames.Add("localhost");
            profile.TrustedClientCaThumbprints.Add("AAAA");
            profileId = profile.Id;

            ctx1.EstProfiles.Add(profile);
            await ctx1.SaveChangesAsync();
        }

        // Mutate the collection in place: no property setter fires, so only the value
        // comparer can make the change tracker notice.
        using (var ctx3 = CreateContext())
        {
            var tracked = await ctx3.EstProfiles.SingleAsync(p => p.Id == profileId);
            tracked.TrustedClientCaThumbprints.Add("BBBB");
            tracked.TrustedClientCaThumbprints.Remove("AAAA");
            await ctx3.SaveChangesAsync();
        }

        await using (var ctx4 = CreateContext())
        {
            var loaded = await ctx4.EstProfiles.SingleAsync(p => p.Id == profileId);
            loaded.TrustedClientCaThumbprints.Should().Equal("BBBB");
        }
    }
}
