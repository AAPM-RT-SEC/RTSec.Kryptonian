using FluentAssertions;
using Moq;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Services;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using Xunit;

namespace RTSec.Kryptonian.Application.Tests.Services;

public class HackathonSettingsServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IHackathonSettingsRepository> _settingsRepoMock;
    private readonly HackathonSettingsService _sut;

    public HackathonSettingsServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _settingsRepoMock = new Mock<IHackathonSettingsRepository>();
        _unitOfWorkMock.Setup(u => u.HackathonSettings).Returns(_settingsRepoMock.Object);

        _sut = new HackathonSettingsService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GetAsyncCreatesDefaultsWhenSettingsDoNotExist()
    {
        _settingsRepoMock.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((HackathonSettings?)null);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.GetAsync();

        result.HarnessBaseUrl.Should().Be("https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io");
        result.DimseHost.Should().Be("kryptonian-dimse.eastus.cloudapp.azure.com");
        _settingsRepoMock.Verify(r => r.Add(It.IsAny<HackathonSettings>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsyncTrimsAndPersistsSettings()
    {
        var settings = new HackathonSettings { Id = Guid.NewGuid() };
        _settingsRepoMock.Setup(r => r.GetSingletonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.UpdateAsync(new HackathonSettingsUpdateDto
        {
            HarnessBaseUrl = "https://harness.example/ ",
            TeamToken = " token ",
            DimseHost = " orthanc.example ",
            DimseTlsPort = 4243,
            OrthancDimsePort = 4242,
            DicomWebBaseUrl = "https://orthanc.example/dicom-web/ ",
            CalledAeTitle = "ORTHANC",
            BridgeAeTitle = "KRYPTONIAN",
            BridgeListenPort = 11112
        });

        result.HarnessBaseUrl.Should().Be("https://harness.example");
        result.TeamToken.Should().Be("token");
        result.DicomWebBaseUrl.Should().Be("https://orthanc.example/dicom-web");
        _settingsRepoMock.Verify(r => r.Update(settings), Times.Once);
    }

    [Fact]
    public async Task UpdateAsyncWithInvalidPortThrowsArgumentException()
    {
        var act = () => _sut.UpdateAsync(new HackathonSettingsUpdateDto
        {
            HarnessBaseUrl = "https://harness.example",
            DimseHost = "orthanc.example",
            DimseTlsPort = 70000,
            OrthancDimsePort = 4242,
            DicomWebBaseUrl = "https://orthanc.example/dicom-web",
            CalledAeTitle = "ORTHANC",
            BridgeAeTitle = "KRYPTONIAN",
            BridgeListenPort = 11112
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*between 1 and 65535*");
    }
}
