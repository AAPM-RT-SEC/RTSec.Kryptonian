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
        _enrollmentEventRepoMock = new Mock<IEnrollmentEventRepository>();
        _certificateRepoMock = new Mock<ICertificateRepository>();
        _connectorMock = new Mock<ICaConnector>();

        _unitOfWorkMock.Setup(u => u.EstProfiles).Returns(_estProfileRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.CaBackends).Returns(_caBackendRepoMock.Object);
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
    public async Task GetCaCertsAsync_WithValidProfile_ReturnsPkcs7()
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
    public async Task GetCaCertsAsync_WithNonExistentProfile_ThrowsInvalidOperationException()
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
    public async Task GetCaCertsAsync_WithDisabledProfile_ThrowsInvalidOperationException()
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
    public async Task EnrollAsync_WithValidCsr_ReturnsSuccessful()
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
                 e.DeviceId == "device-1" &&
                 e.RequestorIpAddress == "192.168.1.1")), Times.Once);
    }

    [Fact]
    public async Task EnrollAsync_WithNonExistentProfile_Returns404()
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
    public async Task EnrollAsync_WithDisabledProfile_Returns403()
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
    public async Task EnrollAsync_WithInvalidCsr_Returns400()
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
    public async Task EnrollAsync_WithInvalidCsrSignature_Returns400()
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
    public async Task EnrollAsync_WhenCaIssuanceFails_Returns500()
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
    public async Task ReenrollAsync_WithValidCertificate_ReturnsSuccessful()
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
    public async Task ReenrollAsync_WithRevokedCertificate_Returns403()
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
    public async Task ReenrollAsync_WithCertificateFromDifferentProfile_Returns403()
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
        return new EstProfile
        {
            Id = id,
            Name = "Test Profile",
            PathPrefix = "/.well-known/est",
            Hostnames = new List<string> { "test.example.com" },
            CaBackendId = backendId,
            ValidityDays = 365,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
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
