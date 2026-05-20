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

    private DeviceService CreateSut()
    {
        return new DeviceService(
            _unitOfWorkMock.Object,
            _orchestratorMock.Object,
            _dataProtectionMock.Object,
            _loggerMock.Object);
    }
}
