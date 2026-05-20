using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
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
    private readonly EnrollmentOrchestrator _sut;
    private readonly X509Certificate2 _testCert;

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

        _unitOfWorkMock.Setup(u => u.EstProfiles).Returns(_estProfileRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.CaBackends).Returns(_caBackendRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Devices).Returns(_deviceRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.EnrollmentEvents).Returns(_enrollmentEventRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Certificates).Returns(_certificateRepoMock.Object);

        _testCert = CreateTestCertificate();

        _sut = new EnrollmentOrchestrator(
            _unitOfWorkMock.Object,
            _connectorFactoryMock.Object,
            _pkcsServiceMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _testCert.Dispose();
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

    #endregion

    #region EnrollAsync Tests

    [Fact]
    public async Task EnrollAsyncWithValidCsrReturnsSuccessful()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=TestDevice", RawData = csrBytes };
        var pkcs7 = new byte[] { 0x30, 0x82 };
        var encodedPkcs7 = new byte[] { 0x65, 0x66 }; // base64 encoded

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
        SetupActiveDeviceAndBackend(backend);

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
            .ReturnsAsync(CertificateIssuanceResult.Successful(_testCert, new[] { _testCert }));

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
        var result = await _sut.EnrollAsync(profileId, csrBytes, "device-1", "192.168.1.1");

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
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=UnknownDevice", RawData = csrBytes };

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
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=PendingDevice", RawData = csrBytes };

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
    public async Task EnrollAsyncWithPendingDeviceAndActivationCodeIssuesCertificateAndConsumesCode()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backend = CreateCaBackend(Guid.NewGuid());
        var profile = CreateEstProfile(profileId, backend.Id);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=PendingDevice", RawData = csrBytes };
        var device = CreatePendingDevice("PendingDevice", "PEND-001", "ACTIVATE123");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
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
            .ReturnsAsync(CertificateIssuanceResult.Successful(_testCert, new[] { _testCert }));

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
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=ActivatedDevice", RawData = csrBytes };
        var device = CreatePendingDevice("pending-alias-device", null, "ACTIVATE123");

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
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
            .ReturnsAsync(CertificateIssuanceResult.Successful(_testCert, new[] { _testCert }));

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
    public async Task EnrollAsyncRoutesToActiveBackendInsteadOfProfileBackend()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profileBackendId = Guid.NewGuid();
        var activeBackendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, profileBackendId);
        var activeBackend = CreateCaBackend(activeBackendId);
        activeBackend.Name = "Active CA";
        activeBackend.IsActive = true;
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=TestDevice", RawData = csrBytes };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        SetupActiveDeviceAndBackend(activeBackend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(true);

        _connectorFactoryMock
            .Setup(f => f.CreateConnector(activeBackend))
            .Returns(_connectorMock.Object);

        _connectorMock
            .Setup(c => c.IssueCertificateAsync(parsedCsr, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CertificateIssuanceResult.Successful(_testCert, new[] { _testCert }));

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
        var result = await _sut.EnrollAsync(profileId, csrBytes, null, null);

        // Assert
        result.Success.Should().BeTrue();
        _connectorFactoryMock.Verify(f => f.CreateConnector(activeBackend), Times.Once);
        _caBackendRepoMock.Verify(r => r.GetByIdAsync(profileBackendId, It.IsAny<CancellationToken>()), Times.Never);
        _certificateRepoMock.Verify(r => r.Add(It.Is<Certificate>(
            c => c.CaBackendId == activeBackendId &&
                 c.CaBackendType == "selfsigned")), Times.Once);
    }

    [Fact]
    public async Task EnrollAsyncWithInvalidCsrSignatureReturns400()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = new byte[] { 1, 2, 3 };
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=Test", RawData = csrBytes };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
        SetupActiveDeviceAndBackend(backend);

        _pkcsServiceMock
            .Setup(p => p.ParsePkcs10(csrBytes))
            .Returns(parsedCsr);

        _pkcsServiceMock
            .Setup(p => p.ValidateCsrSignature(parsedCsr))
            .Returns(false);

        // Act
        var result = await _sut.EnrollAsync(profileId, csrBytes, null, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.ErrorMessage.Should().Contain("signature");
    }

    [Fact]
    public async Task EnrollAsyncWhenCaIssuanceFailsReturns500()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var backendId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, backendId);
        var backend = CreateCaBackend(backendId);
        var csrBytes = CreateTestCsrBytes();
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=TestDevice", RawData = csrBytes };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
        SetupActiveDeviceAndBackend(backend);

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
        var result = await _sut.EnrollAsync(profileId, csrBytes, null, null);

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
        var parsedCsr = new ParsedCsr { SubjectDn = "CN=TestDevice", RawData = csrBytes };
        var pkcs7 = new byte[] { 0x30, 0x82 };
        var encodedPkcs7 = new byte[] { 0x65, 0x66 };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _caBackendRepoMock
            .Setup(r => r.GetByIdAsync(backendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);
        SetupActiveDeviceAndBackend(backend);

        _certificateRepoMock
            .Setup(r => r.GetBySerialNumberAsync(_testCert.SerialNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Certificate?)null); // Certificate not in DB (allowed)

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
            .ReturnsAsync(CertificateIssuanceResult.Successful(_testCert, new[] { _testCert }));

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
        var result = await _sut.ReenrollAsync(profileId, csrBytes, _testCert, "192.168.1.1");

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ReenrollAsyncWithRevokedCertificateReturns403()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = CreateEstProfile(profileId, Guid.NewGuid());
        var existingDbCert = new Certificate
        {
            Id = Guid.NewGuid(),
            SerialNumber = _testCert.SerialNumber,
            EstProfileId = profileId,
            Status = CertificateStatus.Revoked
        };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _certificateRepoMock
            .Setup(r => r.GetBySerialNumberAsync(_testCert.SerialNumber, It.IsAny<CancellationToken>()))
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
        var existingDbCert = new Certificate
        {
            Id = Guid.NewGuid(),
            SerialNumber = _testCert.SerialNumber,
            EstProfileId = differentProfileId, // Different profile!
            Status = CertificateStatus.Valid
        };

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _certificateRepoMock
            .Setup(r => r.GetBySerialNumberAsync(_testCert.SerialNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDbCert);

        // Act
        var result = await _sut.ReenrollAsync(profileId, new byte[] { 1 }, _testCert, null);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
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

    private void SetupActiveDeviceAndBackend(CaBackend backend)
    {
        backend.IsActive = true;

        _caBackendRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(backend);

        _deviceRepoMock
            .Setup(r => r.GetBySubjectCommonNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Device
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test Device",
                SubjectCommonName = "TestDevice",
                Status = DeviceStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
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

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=Test Cert"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));

        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx, "test"),
            "test",
            X509KeyStorageFlags.Exportable);
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

    #endregion
}
