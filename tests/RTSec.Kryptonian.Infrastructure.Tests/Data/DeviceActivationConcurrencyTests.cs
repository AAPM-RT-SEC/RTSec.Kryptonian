using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Infrastructure.Data;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Data;

public class DeviceActivationConcurrencyTests
{
    [Fact]
    public async Task ActivationHashCanOnlyBeConsumedByOneSeparateSqliteContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KryptonianDbContext>()
            .UseSqlite(connection)
            .Options;
        var deviceId = Guid.NewGuid();

        await using (var seed = new KryptonianDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Devices.Add(new Device
            {
                Id = deviceId,
                DisplayName = "Inventory Scanner",
                SubjectCommonName = "scanner-01",
                Status = DeviceStatus.Pending,
                ActivationCodeHash = "0123456789ABCDEF",
                ActivationCodeExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });
            await seed.SaveChangesAsync();
        }

        using var firstContext = new KryptonianDbContext(options);
        using var secondContext = new KryptonianDbContext(options);
        using var first = new UnitOfWork(firstContext);
        using var second = new UnitOfWork(secondContext);
        var firstDevice = (await first.Devices.GetByIdAsync(deviceId))!;
        var secondDevice = (await second.Devices.GetByIdAsync(deviceId))!;

        firstDevice.ActivationCodeHash = null;
        firstDevice.ActivationCodeUsedAt = DateTime.UtcNow;
        firstDevice.ActivationCodeExpiresAt = null;
        first.Devices.Update(firstDevice);
        (await first.TryConsumeActivationCodeAsync(firstDevice)).Should().BeTrue();

        secondDevice.ActivationCodeHash = null;
        secondDevice.ActivationCodeUsedAt = DateTime.UtcNow;
        secondDevice.ActivationCodeExpiresAt = null;
        second.Devices.Update(secondDevice);
        (await second.TryConsumeActivationCodeAsync(secondDevice)).Should().BeFalse();
    }
}
