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

public class CaBackendServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICaBackendRepository> _caBackendRepoMock;
    private readonly Mock<IEstProfileRepository> _estProfileRepoMock;
    private readonly Mock<ICaConnectorFactory> _connectorFactoryMock;
    private readonly Mock<ILogger<CaBackendService>> _loggerMock;
    private readonly IMapper _mapper;
    private readonly CaBackendService _sut;

    public CaBackendServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _caBackendRepoMock = new Mock<ICaBackendRepository>();
        _estProfileRepoMock = new Mock<IEstProfileRepository>();
        _connectorFactoryMock = new Mock<ICaConnectorFactory>();
        _loggerMock = new Mock<ILogger<CaBackendService>>();

        _unitOfWorkMock.Setup(u => u.CaBackends).Returns(_caBackendRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.EstProfiles).Returns(_estProfileRepoMock.Object);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();

        _sut = new CaBackendService(
            _unitOfWorkMock.Object,
            _mapper,
            _loggerMock.Object,
            _connectorFactoryMock.Object);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsyncReturnsAllBackends()
    {
        // Arrange
        var backends = new List<CaBackend>
        {
            new() { Id = Guid.NewGuid(), Name = "Backend 1", Type = CaBackendType.SelfSigned },
            new() { Id = Guid.NewGuid(), Name = "Backend 2", Type = CaBackendType.Acme }
        };
        _caBackendRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(backends);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsyncWithExistingIdReturnsBackend()
    {
        // Arrange
        var id = Guid.NewGuid();
        var backend = new CaBackend { Id = id, Name = "Test", Type = CaBackendType.SelfSigned };
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);

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
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaBackend?)null);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsyncWithValidDtoCreatesBackend()
    {
        // Arrange
        var dto = new CaBackendCreateDto
        {
            Name = "New Backend",
            Type = "selfsigned",
            IsEnabled = true
        };

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Backend");
        result.Type.Should().Be("selfsigned");
        _caBackendRepoMock.Verify(r => r.Add(It.IsAny<CaBackend>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsyncWithInvalidTypeThrowsAutoMapperMappingException()
    {
        // Arrange
        var dto = new CaBackendCreateDto
        {
            Name = "Invalid",
            Type = "invalid_type"
        };

        // Act
        var act = () => _sut.CreateAsync(dto);

        // Assert - AutoMapper wraps the ArgumentException in AutoMapperMappingException
        await act.Should().ThrowAsync<AutoMapper.AutoMapperMappingException>();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsyncWithExistingBackendUpdatesAndReturns()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new CaBackend { Id = id, Name = "Old Name", Type = CaBackendType.SelfSigned };
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new CaBackendUpdateDto { Name = "New Name" };

        // Act
        var result = await _sut.UpdateAsync(id, dto);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        _caBackendRepoMock.Verify(r => r.Update(It.IsAny<CaBackend>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsyncWithNonExistingBackendReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaBackend?)null);

        // Act
        var result = await _sut.UpdateAsync(id, new CaBackendUpdateDto());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsyncWithNoLinkedProfilesDeletes()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new CaBackend { Id = id, Name = "To Delete" };
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _estProfileRepoMock.Setup(r => r.GetByCaBackendIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EstProfile>());
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.DeleteAsync(id);

        // Assert
        result.Should().BeTrue();
        _caBackendRepoMock.Verify(r => r.Delete(existing), Times.Once);
    }

    [Fact]
    public async Task DeleteAsyncWithLinkedProfilesThrowsInvalidOperationException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new CaBackend { Id = id, Name = "Has Profiles" };
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _estProfileRepoMock.Setup(r => r.GetByCaBackendIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EstProfile> { new() });

        // Act
        var act = () => _sut.DeleteAsync(id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*EST profile*");
    }

    [Fact]
    public async Task DeleteAsyncWithNonExistingBackendReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaBackend?)null);

        // Act
        var result = await _sut.DeleteAsync(id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region TestConnectionAsync Tests

    [Fact]
    public async Task TestConnectionAsyncWithSuccessfulConnectionReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new CaBackend { Id = id, Name = "Test", Type = CaBackendType.SelfSigned };
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var connectorMock = new Mock<ICaConnector>();
        connectorMock.Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _connectorFactoryMock.Setup(f => f.CreateConnector(existing))
            .Returns(connectorMock.Object);

        // Act
        var result = await _sut.TestConnectionAsync(id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsyncWithFailedConnectionReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new CaBackend { Id = id, Name = "Test", Type = CaBackendType.SelfSigned };
        _caBackendRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _connectorFactoryMock.Setup(f => f.CreateConnector(existing))
            .Throws(new Exception("Connection failed"));

        // Act
        var result = await _sut.TestConnectionAsync(id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
