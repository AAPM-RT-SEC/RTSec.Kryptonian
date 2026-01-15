using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Mapping;
using RTSec.Kryptonian.Application.Services;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using Xunit;

namespace RTSec.Kryptonian.Application.Tests.Services;

public class EstProfileServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICaBackendRepository> _caBackendRepoMock;
    private readonly Mock<IEstProfileRepository> _estProfileRepoMock;
    private readonly Mock<ILogger<EstProfileService>> _loggerMock;
    private readonly IMapper _mapper;
    private readonly EstProfileService _sut;

    public EstProfileServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _caBackendRepoMock = new Mock<ICaBackendRepository>();
        _estProfileRepoMock = new Mock<IEstProfileRepository>();
        _loggerMock = new Mock<ILogger<EstProfileService>>();

        _unitOfWorkMock.Setup(u => u.CaBackends).Returns(_caBackendRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.EstProfiles).Returns(_estProfileRepoMock.Object);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();

        _sut = new EstProfileService(_unitOfWorkMock.Object, _mapper, _loggerMock.Object);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsyncReturnsAllProfiles()
    {
        // Arrange
        var profiles = new List<EstProfile>
        {
            new() { Id = Guid.NewGuid(), Name = "Profile 1", CaBackendId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), Name = "Profile 2", CaBackendId = Guid.NewGuid() }
        };
        _estProfileRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profiles);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsyncWithExistingIdReturnsProfile()
    {
        // Arrange
        var id = Guid.NewGuid();
        var profile = new EstProfile { Id = id, Name = "Test", CaBackendId = Guid.NewGuid() };
        _estProfileRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetByIdAsyncWithNonExistingIdReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _estProfileRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsyncWithValidDtoCreatesProfile()
    {
        // Arrange
        var caBackendId = Guid.NewGuid();
        var dto = new EstProfileCreateDto
        {
            Name = "New Profile",
            CaBackendId = caBackendId.ToString(),
            Hostnames = new List<string> { "est.example.com" },
            PathPrefix = "/.well-known/est"
        };

        _caBackendRepoMock.Setup(r => r.GetByIdAsync(caBackendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaBackend { Id = caBackendId });
        _estProfileRepoMock.Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Profile");
        _estProfileRepoMock.Verify(r => r.Add(It.IsAny<EstProfile>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsyncWithNonExistingCaBackendThrowsArgumentException()
    {
        // Arrange
        var caBackendId = Guid.NewGuid();
        var dto = new EstProfileCreateDto
        {
            Name = "New Profile",
            CaBackendId = caBackendId.ToString(),
            Hostnames = new List<string> { "est.example.com" }
        };

        _caBackendRepoMock.Setup(r => r.GetByIdAsync(caBackendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaBackend?)null);

        // Act
        var act = () => _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*CA backend not found*");
    }

    [Fact]
    public async Task CreateAsyncWithDuplicatePathHostnameThrowsInvalidOperationException()
    {
        // Arrange
        var caBackendId = Guid.NewGuid();
        var dto = new EstProfileCreateDto
        {
            Name = "New Profile",
            CaBackendId = caBackendId.ToString(),
            Hostnames = new List<string> { "est.example.com" },
            PathPrefix = "/.well-known/est"
        };

        _caBackendRepoMock.Setup(r => r.GetByIdAsync(caBackendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaBackend { Id = caBackendId });
        _estProfileRepoMock.Setup(r => r.GetByPathAndHostnameAsync("/.well-known/est", "est.example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstProfile { Id = Guid.NewGuid() });

        // Act
        var act = () => _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsyncWithExistingProfileUpdatesAndReturns()
    {
        // Arrange
        var id = Guid.NewGuid();
        var caBackendId = Guid.NewGuid();
        var existing = new EstProfile
        {
            Id = id,
            Name = "Old Name",
            CaBackendId = caBackendId,
            Hostnames = new List<string> { "old.example.com" },
            PathPrefix = "/.well-known/est"
        };
        _estProfileRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new EstProfileUpdateDto { Name = "New Name" };

        // Act
        var result = await _sut.UpdateAsync(id, dto);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        _estProfileRepoMock.Verify(r => r.Update(It.IsAny<EstProfile>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsyncWithNonExistingProfileReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _estProfileRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.UpdateAsync(id, new EstProfileUpdateDto());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsyncWithNewCaBackendIdValidatesBackendExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var newCaBackendId = Guid.NewGuid();
        var existing = new EstProfile
        {
            Id = id,
            Name = "Test",
            CaBackendId = Guid.NewGuid(),
            Hostnames = new List<string> { "est.example.com" },
            PathPrefix = "/.well-known/est"
        };
        _estProfileRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(newCaBackendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaBackend?)null);

        var dto = new EstProfileUpdateDto { CaBackendId = newCaBackendId.ToString() };

        // Act
        var act = () => _sut.UpdateAsync(id, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*CA backend not found*");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsyncWithExistingProfileDeletes()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new EstProfile { Id = id, Name = "To Delete" };
        _estProfileRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.DeleteAsync(id);

        // Assert
        result.Should().BeTrue();
        _estProfileRepoMock.Verify(r => r.Delete(existing), Times.Once);
    }

    [Fact]
    public async Task DeleteAsyncWithNonExistingProfileReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _estProfileRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.DeleteAsync(id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
