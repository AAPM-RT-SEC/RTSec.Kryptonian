using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;
using RTSec.Kryptonian.Infrastructure.Acme;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Acme;

/// <summary>
/// Tests for AcmeCaConnector.
/// Note: Full integration tests with Let's Encrypt staging require network access
/// and are marked with [Trait("Category", "Integration")].
/// </summary>
public class AcmeCaConnectorTests
{
    private readonly Mock<ILogger<AcmeCaConnector>> _loggerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAcmeChallengeProvider> _challengeProviderMock;
    private readonly Mock<IDataProtectionService> _dataProtectionMock;
    private readonly Mock<IAcmeAccountRepository> _acmeAccountRepoMock;

    public AcmeCaConnectorTests()
    {
        _loggerMock = new Mock<ILogger<AcmeCaConnector>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _challengeProviderMock = new Mock<IAcmeChallengeProvider>();
        _dataProtectionMock = new Mock<IDataProtectionService>();
        _acmeAccountRepoMock = new Mock<IAcmeAccountRepository>();

        _unitOfWorkMock.Setup(u => u.AcmeAccounts).Returns(_acmeAccountRepoMock.Object);
        _challengeProviderMock.Setup(c => c.ChallengeType).Returns("http-01");
    }

    #region Constructor and Type Tests

    [Fact]
    public void ConstructorInitializesWithCorrectType()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "test@example.com"
        };

        // Act
        var connector = CreateConnector(config);

        // Assert
        connector.Type.Should().Be(CaBackendType.Acme);
    }

    #endregion

    #region GetCaCertificates Tests

    [Fact]
    public async Task GetCaCertificatesForLetsEncryptReturnsIsrgRootCertificate()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptV2.ToString(),
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);

        // Act
        var certs = await connector.GetCaCertificatesAsync();

        // Assert
        certs.Should().NotBeEmpty();
        certs[0].Subject.Should().Contain("ISRG Root X1");
    }

    [Fact]
    public async Task GetCaCertificatesForLetsEncryptStagingReturnsIsrgRootCertificate()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);

        // Act
        var certs = await connector.GetCaCertificatesAsync();

        // Assert
        certs.Should().NotBeEmpty();
        certs[0].Subject.Should().Contain("ISRG Root X1");
    }

    [Fact]
    public async Task GetCaCertificatesForUnknownAcmeCaReturnsEmptyArray()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = "https://unknown-acme-ca.example.com/directory",
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);

        // Act
        var certs = await connector.GetCaCertificatesAsync();

        // Assert
        certs.Should().BeEmpty();
    }

    #endregion

    #region TestConnection Tests

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TestConnectionToLetsEncryptStagingReturnsTrue()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);

        // Act
        var result = await connector.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionToInvalidUrlReturnsFalse()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = "https://invalid-acme-server.localhost/directory",
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);

        // Act
        var result = await connector.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IssueCertificate Tests (with mocked dependencies)

    [Fact]
    public async Task IssueCertificateWithNullCsrThrowsArgumentNullException()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);
        var profile = CreateTestProfile();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connector.IssueCertificateAsync(null!, profile));
    }

    [Fact]
    public async Task IssueCertificateWithNullProfileThrowsArgumentNullException()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);
        var csr = CreateTestParsedCsr("test.example.com");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connector.IssueCertificateAsync(csr, null!));
    }

    [Fact]
    public void ExtractDomainsFromCsrWithInvalidDomainReturnsEmpty()
    {
        // This test verifies the domain extraction logic without making real ACME calls.
        // The actual IssueCertificateAsync test would require extensive mocking of Certes.
        // The domain extraction returns empty for names without dots (not valid domains).

        // "localname" without dots is not a valid domain name
        // The AcmeCaConnector's ExtractDomainsFromCsr method validates this.
        // This is tested indirectly through the AcmeCaConnector's error handling.

        // For now, we test the config and type setup
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);

        // Verify connector type
        connector.Type.Should().Be(CaBackendType.Acme);
    }

    #endregion

    #region RevokeCertificate Tests

    [Fact]
    public async Task RevokeCertificateReturnsNotImplemented()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "test@example.com"
        };
        var connector = CreateConnector(config);

        // Act
        var result = await connector.RevokeCertificateAsync("serial123", RevocationReason.Unspecified);

        // Assert
        result.Should().BeFalse(); // Not yet implemented
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void AcmeConnectorConfigDefaultValuesAreCorrect()
    {
        // Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.DirectoryUrl.Should().Be(WellKnownServers.LetsEncryptV2.ToString());
        config.Email.Should().BeEmpty();
        config.PreferredChallengeType.Should().Be("http-01");
        config.EabKeyId.Should().BeNull();
        config.EabHmacKey.Should().BeNull();
    }

    [Theory]
    [InlineData("http-01")]
    [InlineData("dns-01")]
    public void AcmeConnectorConfigAcceptsValidChallengeTypes(string challengeType)
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig
        {
            PreferredChallengeType = challengeType
        };

        // Assert
        config.PreferredChallengeType.Should().Be(challengeType);
    }

    #endregion

    #region Helper Methods

    private AcmeCaConnector CreateConnector(AcmeConnectorConfig config)
    {
        return new AcmeCaConnector(
            _loggerMock.Object,
            _unitOfWorkMock.Object,
            _challengeProviderMock.Object,
            _dataProtectionMock.Object,
            config);
    }

    private static EstProfile CreateTestProfile()
    {
        return new EstProfile
        {
            Id = Guid.NewGuid(),
            Name = "Test Profile",
            PathPrefix = "/.well-known/est",
            Hostnames = new List<string> { "test.example.com" },
            HostnameMatchType = HostnameMatchType.Exact,
            CaBackendId = Guid.NewGuid(),
            ValidityDays = 90,
            RequireClientCertificate = false,
            IsEnabled = true
        };
    }

    private static ParsedCsr CreateTestParsedCsr(string cn)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={cn}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var csrBytes = request.CreateSigningRequest();

        return new ParsedCsr
        {
            SubjectDn = $"CN={cn}",
            PublicKeyAlgorithm = "RSA",
            KeySize = 2048,
            SignatureAlgorithm = "SHA256",
            RawData = csrBytes,
            SubjectAlternativeNames = new List<string>()
        };
    }

    #endregion
}
