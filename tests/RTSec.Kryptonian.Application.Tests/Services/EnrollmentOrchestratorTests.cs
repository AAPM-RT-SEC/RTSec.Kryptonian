using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Application.Notifications;
using RTSec.Kryptonian.Application.Services;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;
using Xunit;

namespace RTSec.Kryptonian.Application.Tests.Services;

public class EnrollmentOrchestratorTests : IDisposable
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICaConnectorFactory> _connectorFactoryMock;
    private readonly Mock<IPkcsService> _pkcsServiceMock;
    private readonly Mock<ILogger<EnrollmentOrchestrator>> _loggerMock;
    private readonly Mock<IEstProfileRepository> _estProfileRepoMock;
    private readonly Mock<ICaBackendRepository> _caBackendRepoMock;
    private readonly Mock<IDeviceRepository> _deviceRepoMock;
    private readonly Mock<IEnrollmentEventRepository> _enrollmentEventRepoMock;
    private readonly Mock<ICertificateRepository> _certificateRepoMock;
    private readonly Mock<ICaConnector> _connectorMock;
    private readonly Mock<INotificationDispatcher> _notificationDispatcherMock;
    private readonly EnrollmentOrchestrator _sut;
    private readonly X509Certificate2 _testCert;
    private readonly X509Certificate2 _testIssuer;
    private readonly List<X509Certificate2> _issuedCertificates = new();

    public EnrollmentOrchestratorTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _connectorFactoryMock = new Mock<ICaConnectorFactory>();
        _pkcsServiceMock = new Mock<IPkcsService>();
        _loggerMock = new Mock<ILogger<EnrollmentOrchestrator>>();
        _estProfileRepoMock = new Mock<IEstProfileRepository>();
        _caBackendRepoMock = new Mock<ICaBackendRepository>();
        _deviceRepoMock = new Mock<IDeviceRepository>();
        _enrollmentEventRepoMock = new Mock<IEnrollmentEventRepository>();
        _certificateRepoMock = new Mock<ICertificateRepository>();
        _connectorMock = new Mock<ICaConnector>();
        _notificationDispatcherMock = new Mock<INotificationDispatcher>();

        _unitOfWorkMock.Setup(u => u.EstProfiles).Returns(_estProfileRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.CaBackends).Returns(_caBackendRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Devices).Returns(_deviceRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.EnrollmentEvents).Returns(_enrollmentEventRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Certificates).Returns(_certificateRepoMock.Object);
        _unitOfWorkMock
            .Setup(u => u.TryConsumeActivationCodeAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _testCert = CreateTestCertificate();
        _testIssuer = CreateTestCertificate("Issuer", isCa: true);
        _connectorMock.Setup(c => c.GetCaCertificatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { _testIssuer });

        _sut = new EnrollmentOrchestrator(
            _unitOfWorkMock.Object,
            _connectorFactoryMock.Object,
            _pkcsServiceMock.Object,
            _notificationDispatcherMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _testCert.Dispose();
        _testIssuer.Dispose();
        foreach (var cert in _issuedCertificates) cert.Dispose();
    }

    #region GetCaCertsAsync Tests

    [Fact]
    public async Task GetCaCertsAsyncWithValidProfileReturnsPkcs7()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var caCerts = new[] { _testCert };
        var pkcs7 = new byte[] { 0x30, 0x82, 0x01 };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(backend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.GetCaCertificatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(caCerts);

        _pkcsServiceMock
            .Setup(p => p.EncodeToPkcs7(caCerts))
            .Returns(pkcs7);

        // Act
        var result = await _sut.GetCaCertsAsync(profileId);

        // Assert
        result.Should().BeEquivalentTo(pkcs7);
    }

    [Fact]
    public async Task GetCaCertsAsyncWithNonExistentProfileThrowsInvalidOperationException()
    {
        // Arrange
        var profileId = Guid.NewGuid();

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var act = () => _sut.GetCaCertsAsync(profileId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetCaCertsAsyncWithDisabledProfileThrowsInvalidOperationException()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid(), isEnabled: false);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var act = () => _sut.GetCaCertsAsync(profileId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*disabled*");
    }

    [Fact]
    public async Task GetCaCertsReturnsProfileBackendCaEvenWhenADifferentBackendIsActive()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profileBackendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, profileBackendId);
        var profileBackend = CreateCaBackend(profileBackendId);
        var activeBackend = CreateCaBackend(Guid.NewGuid());
        activeBackend.Name = "Unrelated Active CA";
        activeBackend.IsActive = true;
        var caCerts = new[] { _testCert };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        SetupActiveBackend(activeBackend);
        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(profileBackendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileBackend);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(profileBackend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.GetCaCertificatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(caCerts);

        _pkcsServiceMock
            .Setup(p => p.EncodeToPkcs7(caCerts))
            .Returns(new byte[] { 0x30 });

        // Act
        await _sut.GetCaCertsAsync(profileId);

        // Assert: the profile's own CA answered, and the global lookup was never consulted.
        _connectorFactoryMock.Verify(f => f.CreateConnector(profileBackend), Times.Once);
        _connectorFactoryMock.Verify(f => f.CreateConnector(activeBackend), Times.Never);
        _caBackendRepoMock.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region EnrollAsync Tests

    [Fact]
    public async Task EnrollAdminAsyncWithValidCsrReturnsSuccessful()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=TestDevice");
        var pkcs7 = new byte[] { 0x30, 0x82 };
        var encodedPkcs7 = new byte[] { 0x65, 0x66 }; // base64 encoded

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
        var device = SetupAdminDeviceAndBackend(backend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(backend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.IssueCertificateAsync(parsedCsr, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateIssuedCertificate(parsedCsr));

        _pkcsServiceMock
            .Setup(p => p.ExportToPem(_testCert))
            .Returns("-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----");

        _pkcsServiceMock
            .Setup(p => p.EncodeToPkcs7(It.IsAny<X509Certificate2[]>()))
            .Returns(pkcs7);

        _pkcsServiceMock
            .Setup(p => p.EncodeEstResponseBody(pkcs7))
            .Returns(encodedPkcs7);

        // Act
        var result = await _sut.EnrollAdminAsync(profileId, device.Id, csrBytes, "192.168.1.1");

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Pkcs7Response.Should().BeEquivalentTo(encodedPkcs7);

        // Verify enrollment event was created
        _enrollmentEventRepoMock.Verify(r => r.Add(It.Is<EnrollmentEvent>(
            e => e.ProfileId == profileId &&
                 e.DeviceId == "TestDevice" &&
                 e.RequestorIpAddress == "192.168.1.1")), Times.Once);
    }

    [Fact]
    public async Task EnrollAsyncWithNonExistentProfileReturns404()
    {
        // Arrange
        var profileId = Guid.NewGuid();

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.EnrollAsync(profileId, new byte[] { 1, 2, 3 }, null, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task EnrollAsyncWithActiveDeviceCommonNameRejectsBeforeConnectorDispatch()
    {
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=TestDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _deviceRepoMock
            .Setup(r => r.GetBySubjectCommonNameAsync("TestDevice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateActiveDevice("TestDevice"));
        _pkcsServiceMock.Setup(p => p.ParsePkcs10(csrBytes)).Returns(parsedCsr);

        var result = await _sut.EnrollAsync(profileId, csrBytes, null, null);

        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("authenticated re-enrollment");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsyncWithDisabledProfileReturns403()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid(), isEnabled: false);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _sut.EnrollAsync(profileId, new byte[] { 1, 2, 3 }, null, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task EnrollAsyncWithInvalidCsrReturns400()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = new byte[] { 1, 2, 3 };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
        SetupActiveDeviceAndBackend(backend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Throws(new ArgumentException("Invalid CSR"));

        // Act
        var result = await _sut.EnrollAsync(profileId, csrBytes, null, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task EnrollAsyncWithUnknownDeviceRejectsBeforeConnectorDispatch()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = CreateParsedCsr("CN=UnknownDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _deviceRepoMock
            .Setup(r => r.GetBySubjectCommonNameAsync("UnknownDevice", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);

        // Act
        var result = await _sut.EnrollAsync(profileId, csrBytes, null, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("Unknown device");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
        _enrollmentEventRepoMock.Verify(r => r.Add(It.Is<EnrollmentEvent>(
            e => e.Status == EnrollmentStatus.Rejected &&
                 e.ErrorMessage == "Unknown device subject common name")), Times.Once);
    }

    [Fact]
    public async Task EnrollAsyncWithPendingDeviceRequiresActivationCode()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backend = CreateCaBackend(Guid.NewGuid());
        var profile = CreateEstProfile(profileId, backend.Id);
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = CreateParsedCsr("CN=PendingDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _deviceRepoMock
            .Setup(r => r.GetBySubjectCommonNameAsync("PendingDevice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePendingDevice("PendingDevice", "PEND-001", "ACTIVATE123"));

        // Act
        var result = await _sut.EnrollAsync(profileId, csrBytes, null, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("activation code");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsyncWithBasicDeviceIdAndWrongActivationCodeRejectsBeforeConnectorDispatch()
    {
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = CreateParsedCsr("CN=InventoryScanner");
        var device = CreatePendingDevice("InventoryScanner", "INV-001", "ACTIVATE123");

        _estProfileRepoMock.Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _deviceRepoMock.Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        _deviceRepoMock.Setup(r => r.GetBySubjectCommonNameAsync("InventoryScanner", It.IsAny<CancellationToken>())).ReturnsAsync(device);
        _pkcsServiceMock.Setup(p => p.ParsePkcs10(csrBytes)).Returns(parsedCsr);

        var result = await _sut.EnrollAsync(
            profileId,
            csrBytes,
            device.Id.ToString("D"),
            null,
            activationCode: "WRONG-CODE");

        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("Invalid activation code");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsyncWithBasicDeviceIdAndExpiredActivationCodeRejectsBeforeConnectorDispatch()
    {
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = CreateParsedCsr("CN=InventoryScanner");
        var device = CreatePendingDevice("InventoryScanner", "INV-001", "ACTIVATE123");
        device.ActivationCodeExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        _estProfileRepoMock.Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _deviceRepoMock.Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        _deviceRepoMock.Setup(r => r.GetBySubjectCommonNameAsync("InventoryScanner", It.IsAny<CancellationToken>())).ReturnsAsync(device);
        _pkcsServiceMock.Setup(p => p.ParsePkcs10(csrBytes)).Returns(parsedCsr);

        var result = await _sut.EnrollAsync(
            profileId,
            csrBytes,
            device.Id.ToString("D"),
            null,
            activationCode: "ACTIVATE123");

        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("expired");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsyncWithBasicDeviceIdDoesNotReplacePreassignedPendingScannerCommonName()
    {
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = CreateParsedCsr("CN=AnotherDevice");
        var device = CreatePendingDevice("pending-scanner", "INV-001", "ACTIVATE123");

        _estProfileRepoMock.Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _deviceRepoMock.Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        _pkcsServiceMock.Setup(p => p.ParsePkcs10(csrBytes)).Returns(parsedCsr);

        var result = await _sut.EnrollAsync(
            profileId,
            csrBytes,
            device.Id.ToString("D"),
            null,
            activationCode: "ACTIVATE123");

        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("common name does not match");
        _deviceRepoMock.Verify(r => r.GetBySubjectCommonNameAsync("AnotherDevice", It.IsAny<CancellationToken>()), Times.Never);
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsyncWithBasicDeviceIdRejectsActivationCodeConcurrencyConflictBeforeConnectorDispatch()
    {
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = CreateParsedCsr("CN=InventoryScanner");
        var device = CreatePendingDevice("InventoryScanner", "INV-001", "ACTIVATE123");

        _estProfileRepoMock.Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _deviceRepoMock.Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        _deviceRepoMock.Setup(r => r.GetBySubjectCommonNameAsync("InventoryScanner", It.IsAny<CancellationToken>())).ReturnsAsync(device);
        _pkcsServiceMock.Setup(p => p.ParsePkcs10(csrBytes)).Returns(parsedCsr);
        _pkcsServiceMock.Setup(p => p.ValidateCsrSignature(parsedCsr)).Returns(true);
        _unitOfWorkMock
            .Setup(u => u.TryConsumeActivationCodeAsync(device, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.EnrollAsync(
            profileId,
            csrBytes,
            device.Id.ToString("D"),
            null,
            activationCode: "ACTIVATE123");

        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("already been used");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsyncWithPendingDeviceAndActivationCodeIssuesCertificateAndConsumesCode()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backend = CreateCaBackend(Guid.NewGuid());
        var profile = CreateEstProfile(profileId, backend.Id);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=PendingDevice");
        var device = CreatePendingDevice("PendingDevice", "PEND-001", "ACTIVATE123");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backend.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);

        _deviceRepoMock
            .Setup(r => r.GetBySubjectCommonNameAsync("PendingDevice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(backend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.IssueCertificateAsync(parsedCsr, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateIssuedCertificate(parsedCsr));

        _pkcsServiceMock
            .Setup(p => p.ExportToPem(_testCert))
            .Returns("-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----");

        _pkcsServiceMock
            .Setup(p => p.EncodeToPkcs7(It.IsAny<X509Certificate2[]>()))
            .Returns([0x30]);

        _pkcsServiceMock
            .Setup(p => p.EncodeEstResponseBody(It.IsAny<byte[]>()))
            .Returns([0x31]);

        // Act
        var result = await _sut.EnrollAsync(
            profileId,
            csrBytes,
            null,
            null,
            activationCode: "ACTIVATE123",
            activationSerialNumber: "PEND-001");

        // Assert
        result.Success.Should().BeTrue();
        device.Status.Should().Be(DeviceStatus.Active);
        device.ActivationCodeUsedAt.Should().NotBeNull();
        device.ActivationCodeHash.Should().BeNull();
        device.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EnrollAsyncWithActivationCodeFindsPendingAliasAndBindsDeviceIdentity()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backend = CreateCaBackend(Guid.NewGuid());
        var profile = CreateEstProfile(profileId, backend.Id);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=ActivatedDevice");
        var device = CreatePendingDevice("placeholder", null, "ACTIVATE123");
        device.SubjectCommonName = $"pending-{device.Id:N}";

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backend.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);

        _deviceRepoMock
            .SetupSequence(r => r.GetBySubjectCommonNameAsync("ActivatedDevice", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null)
            .ReturnsAsync((Device?)null);

        _deviceRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device });

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(backend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.IssueCertificateAsync(parsedCsr, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateIssuedCertificate(parsedCsr));

        _pkcsServiceMock
            .Setup(p => p.ExportToPem(_testCert))
            .Returns("-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----");

        _pkcsServiceMock
            .Setup(p => p.EncodeToPkcs7(It.IsAny<X509Certificate2[]>()))
            .Returns([0x30]);

        _pkcsServiceMock
            .Setup(p => p.EncodeEstResponseBody(It.IsAny<byte[]>()))
            .Returns([0x31]);

        // Act
        var result = await _sut.EnrollAsync(
            profileId,
            csrBytes,
            null,
            null,
            activationCode: "ACTIVATE123",
            activationManufacturer: "RTSec",
            activationModel: "Demo Model",
            activationSerialNumber: "SER-123");

        // Assert
        result.Success.Should().BeTrue();
        device.SubjectCommonName.Should().Be("ActivatedDevice");
        device.Manufacturer.Should().Be("RTSec");
        device.Model.Should().Be("Demo Model");
        device.SerialNumber.Should().Be("SER-123");
        device.Status.Should().Be(DeviceStatus.Active);
    }

    [Fact]
    public async Task EnrollAdminAsyncRoutesToProfileBackendEvenWhenADifferentBackendIsActive()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profileBackendId = Guid.NewGuid();
        var activeBackendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, profileBackendId);
        var profileBackend = CreateCaBackend(profileBackendId);
        profileBackend.Name = "Profile CA";
        var activeBackend = CreateCaBackend(activeBackendId);
        activeBackend.Name = "Unrelated Active CA";
        activeBackend.IsActive = true;
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=TestDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // The globally active backend is a different, conflicting CA.
        SetupActiveBackend(activeBackend);
        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(profileBackendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileBackend);

        var device = CreateActiveDevice("TestDevice");
        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(profileBackend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.IssueCertificateAsync(parsedCsr, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateIssuedCertificate(parsedCsr));

        _pkcsServiceMock
            .Setup(p => p.ExportToPem(_testCert))
            .Returns("-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----");

        _pkcsServiceMock
            .Setup(p => p.EncodeToPkcs7(It.IsAny<X509Certificate2[]>()))
            .Returns([0x30]);

        _pkcsServiceMock
            .Setup(p => p.EncodeEstResponseBody(It.IsAny<byte[]>()))
            .Returns([0x31]);

        // Act
        var result = await _sut.EnrollAdminAsync(profileId, device.Id, csrBytes, null);

        // Assert: issuance used the profile's own CA, never the globally active one.
        result.Success.Should().BeTrue();
        _connectorFactoryMock.Verify(f => f.CreateConnector(profileBackend), Times.Once);
        _connectorFactoryMock.Verify(f => f.CreateConnector(activeBackend), Times.Never);
        _caBackendRepoMock.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
        _certificateRepoMock.Verify(r => r.Add(It.Is<Certificate>(
            c => c.CaBackendId == profileBackendId &&
                 c.CaBackendType == "selfsigned")), Times.Once);
    }

    [Fact]
    public async Task EnrollAdminAsyncWhenProfileBackendMissingReturns503WithoutConsultingActiveBackend()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var missingBackendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, missingBackendId);
        var unrelatedActive = CreateCaBackend(Guid.NewGuid());
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=TestDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var device = SetupAdminDeviceAndBackend(unrelatedActive);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        // Act
        var result = await _sut.EnrollAdminAsync(profileId, device.Id, csrBytes, null);

        // Assert: no silent failover to the active backend.
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.ErrorMessage.Should().Contain("EST profile");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAdminAsyncWhenProfileBackendDisabledReturns503()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var disabledBackend = CreateCaBackend(backendId, isEnabled: false);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=TestDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(disabledBackend);

        var device = SetupAdminDeviceAndBackend(disabledBackend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        // Act
        var result = await _sut.EnrollAdminAsync(profileId, device.Id, csrBytes, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.ErrorMessage.Should().Contain("disabled");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAdminAsyncWithInvalidCsrSignatureReturns400()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = CreateParsedCsr("CN=TestDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
        var device = SetupAdminDeviceAndBackend(backend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(false);

        // Act
        var result = await _sut.EnrollAdminAsync(profileId, device.Id, csrBytes, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.ErrorMessage.Should().Contain("signature");
    }

    [Fact]
    public async Task EnrollAdminAsyncWhenCaIssuanceFailsReturns500()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=TestDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
        var device = SetupAdminDeviceAndBackend(backend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(backend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.IssueCertificateAsync(parsedCsr, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CertificateIssuanceResult.Failed("CA error"));

        // Act
        var result = await _sut.EnrollAdminAsync(profileId, device.Id, csrBytes, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(500);

        // Verify enrollment event was updated to error status
        _enrollmentEventRepoMock.Verify(r => r.Update(It.Is<EnrollmentEvent>(
            e => e.Status == EnrollmentStatus.Error)), Times.Once);
    }

    #endregion

    #region ReenrollAsync Tests

    [Fact]
    public async Task ReenrollAsyncWithValidCertificateReturnsSuccessful()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = CreateParsedCsr("CN=TestDevice");
        var pkcs7 = new byte[] { 0x30, 0x82 };
        var encodedPkcs7 = new byte[] { 0x65, 0x66 };
        var device = CreateActiveDevice("TestDevice");
        var existingDbCert = CreateStoredCertificate(_testCert, profileId, device);
        Certificate? storedRenewedCertificate = null;

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _certificateRepoMock
            .Setup(r => r.GetByThumbprintAsync(_testCert.GetCertHashString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDbCert);

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        SetupProfileBackend(backend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(backend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.IssueCertificateAsync(parsedCsr, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateIssuedCertificate(parsedCsr));

        _pkcsServiceMock
            .Setup(p => p.ExportToPem(_testCert))
            .Returns("-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----");

        _pkcsServiceMock
            .Setup(p => p.EncodeToPkcs7(It.IsAny<X509Certificate2[]>()))
            .Returns(pkcs7);

        _pkcsServiceMock
            .Setup(p => p.EncodeEstResponseBody(pkcs7))
            .Returns(encodedPkcs7);

        _certificateRepoMock
            .Setup(r => r.Add(It.IsAny<Certificate>()))
            .Callback<Certificate>(c => storedRenewedCertificate = c);

        // Act
        var result = await _sut.ReenrollAsync(profileId, csrBytes, _testCert, "192.168.1.1");

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Pkcs7Response.Should().BeEquivalentTo(encodedPkcs7);
        storedRenewedCertificate.Should().NotBeNull();
        device.LastCertificateId.Should().Be(storedRenewedCertificate!.Id);
        existingDbCert.Status.Should().Be(CertificateStatus.Valid);
    }

    [Fact]
    public async Task ReenrollAsyncWithRevokedCertificateReturns403()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var existingDbCert = CreateStoredCertificate(_testCert, profileId, CreateActiveDevice("TestDevice"), CertificateStatus.Revoked);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _certificateRepoMock
            .Setup(r => r.GetByThumbprintAsync(_testCert.GetCertHashString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDbCert);

        // Act
        var result = await _sut.ReenrollAsync(profileId, new byte[] { 1 }, _testCert, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("revoked");
    }

    [Fact]
    public async Task ReenrollAsyncWithCertificateFromDifferentProfileReturns403()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var differentProfileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var existingDbCert = CreateStoredCertificate(_testCert, differentProfileId, CreateActiveDevice("TestDevice"));

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _certificateRepoMock
            .Setup(r => r.GetByThumbprintAsync(_testCert.GetCertHashString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDbCert);

        // Act
        var result = await _sut.ReenrollAsync(profileId, new byte[] { 1 }, _testCert, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ReenrollAsyncWithForgedCertificateDoesNotFallBackToSerialNumber()
    {
        using var forgedCert = CreateTestCertificate("TestDevice");
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var device = CreateActiveDevice("TestDevice");
        var storedCert = CreateStoredCertificate(_testCert, profileId, device);
        storedCert.SerialNumber = forgedCert.SerialNumber;

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _certificateRepoMock
            .Setup(r => r.GetByThumbprintAsync(forgedCert.GetCertHashString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Certificate?)null);
        _certificateRepoMock
            .Setup(r => r.GetBySerialNumberAsync(forgedCert.SerialNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedCert);

        var result = await _sut.ReenrollAsync(profileId, new byte[] { 1 }, forgedCert, null);

        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("not registered");
        _certificateRepoMock.Verify(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task ReenrollAsyncWithExpiredCertificateReturns403()
    {
        // Arrange
        using var expiredCert = CreateTestCertificate(
            notBefore: DateTimeOffset.UtcNow.AddDays(-2),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));

        // Act
        var result = await _sut.ReenrollAsync(Guid.NewGuid(), new byte[] { 1 }, expiredCert, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("expired");
    }

    [Fact]
    public async Task ReenrollAsyncWithNotYetValidCertificateReturns403()
    {
        // Arrange
        using var futureCert = CreateTestCertificate(
            notBefore: DateTimeOffset.UtcNow.AddDays(1),
            notAfter: DateTimeOffset.UtcNow.AddDays(2));

        // Act
        var result = await _sut.ReenrollAsync(Guid.NewGuid(), new byte[] { 1 }, futureCert, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("not yet valid");
    }

    [Fact]
    public async Task ReenrollAsyncWithCsrCommonNameMismatchReturns403()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var device = CreateActiveDevice("TestDevice");
        var existingDbCert = CreateStoredCertificate(_testCert, profileId, device);
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = CreateParsedCsr("CN=OtherDevice");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _certificateRepoMock
            .Setup(r => r.GetByThumbprintAsync(_testCert.GetCertHashString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDbCert);

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        // Act
        var result = await _sut.ReenrollAsync(profileId, csrBytes, _testCert, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("does not match");
        _connectorFactoryMock.Verify(f => f.CreateConnector(It.IsAny<CaBackend>()), Times.Never);
    }

    [Fact]
    public async Task ReenrollAsyncWithRemovedDeviceReturns403()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var device = CreateActiveDevice("TestDevice");
        device.Status = DeviceStatus.Removed;
        device.RemovedAt = DateTime.UtcNow;
        var existingDbCert = CreateStoredCertificate(_testCert, profileId, device);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _certificateRepoMock
            .Setup(r => r.GetByThumbprintAsync(_testCert.GetCertHashString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDbCert);

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        // Act
        var result = await _sut.ReenrollAsync(profileId, new byte[] { 1 }, _testCert, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorMessage.Should().Contain("Device is not active");
    }

    #endregion

    #region Helper Methods

    private static EstProfile CreateEstProfile(
        Guid id,
        Guid backendId,
        bool isEnabled = true)
    {
        var profile = new EstProfile
        {
            Id = id,
            Name = "Test Profile",
            PathPrefix = "/.well-known/est",
            CaBackendId = backendId,
            ValidityDays = 365,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        profile.Hostnames.Add("test.example.com");
        profile.AllowedKeyUsages.AddRange(["digitalSignature", "clientAuth", "serverAuth"]);
        return profile;
    }

    private static CaBackend CreateCaBackend(Guid id, bool isEnabled = true)
    {
        return new CaBackend
        {
            Id = id,
            Name = "Test CA",
            Type = CaBackendType.SelfSigned,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Stubs the backend resolved via <see cref="EstProfile.CaBackendId"/> (GetByIdAsync) and an
    /// active device. Enrollment no longer consults the globally active backend, so this is the
    /// only backend stub production code will observe.
    /// </summary>
    private void SetupProfileBackend(CaBackend backend)
    {
        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backend.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
    }

    private void SetupActiveDeviceAndBackend(CaBackend backend)
    {
        SetupProfileBackend(backend);

        _deviceRepoMock
            .Setup(r => r.GetBySubjectCommonNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateActiveDevice("TestDevice"));
    }

    private Device SetupAdminDeviceAndBackend(CaBackend backend)
    {
        SetupProfileBackend(backend);
        var device = CreateActiveDevice("TestDevice");
        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
        return device;
    }

    /// <summary>
    /// Stubs a conflicting globally active backend. Used only by tests that must prove
    /// enrollment ignores it.
    /// </summary>
    private void SetupActiveBackend(CaBackend backend)
    {
        backend.IsActive = true;

        _caBackendRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
    }

    private static Device CreatePendingDevice(string commonName, string? serialNumber, string activationCode)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DisplayName = "Pending Device",
            SubjectCommonName = commonName,
            SerialNumber = serialNumber,
            Status = DeviceStatus.Pending,
            ActivationCodeExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        device.ActivationCodeHash = DeviceService.HashActivationCode(activationCode, device.Id);
        return device;
    }

    private static Device CreateActiveDevice(string commonName) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Test Device",
        SubjectCommonName = commonName,
        Status = DeviceStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Certificate CreateStoredCertificate(
        X509Certificate2 certificate,
        Guid profileId,
        Device device,
        CertificateStatus status = CertificateStatus.Valid) => new()
    {
        Id = Guid.NewGuid(),
        SerialNumber = certificate.SerialNumber,
        SubjectDn = $"CN={device.SubjectCommonName}",
        IssuerDn = certificate.Issuer,
        Thumbprint = certificate.GetCertHashString(),
        NotBefore = certificate.NotBefore.ToUniversalTime(),
        NotAfter = certificate.NotAfter.ToUniversalTime(),
        CertificatePem = "-----BEGIN CERTIFICATE-----\ntest\n-----END CERTIFICATE-----",
        Status = status,
        EstProfileId = profileId,
        DeviceId = device.SubjectCommonName,
        DeviceRecordId = device.Id,
        CertificateDerBase64 = Convert.ToBase64String(certificate.RawData),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static X509Certificate2 CreateTestCertificate(
        string commonName = "TestDevice",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        bool isCa = false)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={commonName}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isCa, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(isCa ? X509KeyUsageFlags.KeyCertSign : X509KeyUsageFlags.DigitalSignature, true));
        var cert = request.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-5),
            notAfter ?? DateTimeOffset.UtcNow.AddYears(1));

        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx, "test"),
            "test",
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    private static byte[] CreateTestCsrBytes()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=TestDevice"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSigningRequest();
    }

    private ParsedCsr CreateParsedCsr(string subject)
    {
        using var key = _testCert.GetRSAPrivateKey()!;
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new ParsedCsr { SubjectDn = subject, RawData = request.CreateSigningRequest() };
    }

    private CertificateIssuanceResult CreateIssuedCertificate(ParsedCsr csr)
    {
        var request = CertificateRequest.LoadSigningRequest(csr.RawData.ToArray(), HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1"), new("1.3.6.1.5.5.7.3.2") }, false));
        var leaf = request.Create(_testIssuer, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(30), RandomNumberGenerator.GetBytes(16));
        _issuedCertificates.Add(leaf);
        return CertificateIssuanceResult.Successful(leaf, new[] { leaf, _testIssuer });
    }

    #endregion
}
