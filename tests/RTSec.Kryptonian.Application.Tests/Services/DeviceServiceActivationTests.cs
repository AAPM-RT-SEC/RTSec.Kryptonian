using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using Xunit;

namespace RTSec.Kryptonian.Application.Tests.Services;

public class DeviceServiceActivationTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IDeviceRepository> _deviceRepoMock = new();
    private readonly Mock<IEnrollmentOrchestrator> _orchestratorMock = new();
    private readonly Mock<IDataProtectionService> _dataProtectionMock = new();
    private readonly Mock<ILogger<DeviceService>> _loggerMock = new();

    public DeviceServiceActivationTests()
    {
        _unitOfWorkMock.Setup(u => u.Devices).Returns(_deviceRepoMock.Object);
    }

    [Fact]
    public async Task GenerateActivationCodeAsyncForPendingDeviceStoresHashAndReturnsPlaintextOnce()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DisplayName = "Scanner",
            SubjectCommonName = "pending-device",
            Status = DeviceStatus.Pending
        };

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var result = await sut.GenerateActivationCodeAsync(device.Id, new DeviceActivationCodeCreateDto
        {
            ValidForMinutes = 5
        });

        result.Should().NotBeNull();
        result!.ActivationCode.Should().HaveLength(20);
        result.SubjectCommonName.Should().Be("pending-device");
        result.SerialNumber.Should().BeEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        device.ActivationCodeHash.Should().NotBeNullOrWhiteSpace();
        device.ActivationCodeHash.Should().NotBe(result.ActivationCode);
        device.ActivationCodeExpiresAt.Should().Be(result.ExpiresAt);
        device.ActivationCodeUsedAt.Should().BeNull();
        _deviceRepoMock.Verify(r => r.Update(device), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateActivationCodeAsyncRejectsInvalidLifetime()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DisplayName = "Scanner",
            SubjectCommonName = "pending-device",
            Status = DeviceStatus.Pending
        };

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var act = () => sut.GenerateActivationCodeAsync(device.Id, new DeviceActivationCodeCreateDto
        {
            ValidForMinutes = 0
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*lifetime*");
    }

    [Fact]
    public async Task ReactivateActivationCodeAsyncForExpiredUnusedCodeStoresNewHashAndReturnsPlaintext()
    {
        var device = CreatePendingDeviceWithActivationCode();
        var previousHash = device.ActivationCodeHash;

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var result = await sut.ReactivateActivationCodeAsync(device.Id, new DeviceActivationCodeCreateDto
        {
            ValidForMinutes = 10
        });

        result.Should().NotBeNull();
        result!.ActivationCode.Should().HaveLength(20);
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        device.ActivationCodeHash.Should().NotBeNullOrWhiteSpace();
        device.ActivationCodeHash.Should().NotBe(previousHash);
        device.ActivationCodeUsedAt.Should().BeNull();
        device.ActivationCodeExpiresAt.Should().Be(result.ExpiresAt);
        device.Status.Should().Be(DeviceStatus.Pending);
        _deviceRepoMock.Verify(r => r.Update(device), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReactivateActivationCodeAsyncRejectsStillValidCode()
    {
        var device = CreatePendingDeviceWithActivationCode(expiresAt: DateTime.UtcNow.AddMinutes(5));

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var act = () => sut.ReactivateActivationCodeAsync(device.Id, new DeviceActivationCodeCreateDto());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*has not expired*");
        _deviceRepoMock.Verify(r => r.Update(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task ReactivateActivationCodeAsyncRejectsUsedCode()
    {
        var device = CreatePendingDeviceWithActivationCode();
        device.ActivationCodeUsedAt = DateTime.UtcNow.AddMinutes(-1);

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var act = () => sut.ReactivateActivationCodeAsync(device.Id, new DeviceActivationCodeCreateDto());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been used*");
        _deviceRepoMock.Verify(r => r.Update(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task ReactivateActivationCodeAsyncRejectsDeviceWithoutCode()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DisplayName = "Scanner",
            SubjectCommonName = "pending-device",
            Status = DeviceStatus.Pending
        };

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var act = () => sut.ReactivateActivationCodeAsync(device.Id, new DeviceActivationCodeCreateDto());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not have an activation code*");
        _deviceRepoMock.Verify(r => r.Update(It.IsAny<Device>()), Times.Never);
    }

    [Theory]
    [InlineData(DeviceStatus.Active)]
    [InlineData(DeviceStatus.Removed)]
    public async Task ReactivateActivationCodeAsyncRejectsNonPendingDevice(DeviceStatus status)
    {
        var device = CreatePendingDeviceWithActivationCode();
        device.Status = status;

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var act = () => sut.ReactivateActivationCodeAsync(device.Id, new DeviceActivationCodeCreateDto());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pending devices*");
        _deviceRepoMock.Verify(r => r.Update(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task PurgeAsyncDeletesRemovedDevice()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DisplayName = "Retired Scanner",
            SubjectCommonName = "retired-scanner",
            Status = DeviceStatus.Removed
        };

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var result = await sut.PurgeAsync(device.Id);

        result.Should().BeTrue();
        _deviceRepoMock.Verify(r => r.Delete(device), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeAsyncRejectsActiveDevice()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DisplayName = "Scanner",
            SubjectCommonName = "scanner",
            Status = DeviceStatus.Active
        };

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        var act = () => sut.PurgeAsync(device.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*archived devices*");
        _deviceRepoMock.Verify(r => r.Delete(It.IsAny<Device>()), Times.Never);
    }

    private DeviceService CreateSut()
    {
        return new DeviceService(
            _unitOfWorkMock.Object,
            _orchestratorMock.Object,
            _dataProtectionMock.Object,
            _loggerMock.Object);
    }

    private static Device CreatePendingDeviceWithActivationCode(DateTime? expiresAt = null)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DisplayName = "Scanner",
            SubjectCommonName = "pending-device",
            Status = DeviceStatus.Pending,
            ActivationCodeExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(-5),
            ActivationCodeUsedAt = null
        };
        device.ActivationCodeHash = DeviceService.HashActivationCode("OLD-CODE", device.Id);
        return device;
    }
}
