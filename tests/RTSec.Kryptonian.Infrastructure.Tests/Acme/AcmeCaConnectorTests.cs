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
using AcmeCertificateChain = Certes.Acme.CertificateChain;

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
        // Arrange
        var csr = CreateTestParsedCsr("localname");

        // Act
        var domains = AcmeCaConnector.ExtractDomainsFromCsr(csr);

        // Assert
        domains.Should().BeEmpty();
    }

    [Fact]
    public void ExtractDomainsFromCsrReturnsCnAndUniqueDnsSans()
    {
        // Arrange
        var csr = new ParsedCsr
        {
            SubjectDn = "CN=device.example.com",
            PublicKeyAlgorithm = "RSA",
            KeySize = 2048,
            RawData = new byte[] { 0x30, 0x00 },
            SubjectAlternativeNames = new[]
            {
                "DNS:device.example.com",
                "DNS:alt.example.com",
                "IP:192.0.2.10",
                "localname"
            }
        };

        // Act
        var domains = AcmeCaConnector.ExtractDomainsFromCsr(csr);

        // Assert
        domains.Should().Equal("device.example.com", "alt.example.com");
    }

    [Fact]
    public void IsAllowedByProfileAcceptsMatchingProfileHostname()
    {
        // Arrange
        var profile = CreateTestProfile();

        // Act
        var allowed = AcmeCaConnector.IsAllowedByProfile(profile, "test.example.com");

        // Assert
        allowed.Should().BeTrue();
    }

    [Fact]
    public void IsAllowedByProfileRejectsHostnameOutsideProfilePolicy()
    {
        // Arrange
        var profile = CreateTestProfile();

        // Act
        var allowed = AcmeCaConnector.IsAllowedByProfile(profile, "other.example.com");

        // Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public void ParseCertificateChainDoesNotAttachPrivateKey()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=device.example.com"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var sourceCert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(30));
        var chain = new AcmeCertificateChain(sourceCert.ExportCertificatePem());

        // Act
        var parsed = AcmeCaConnector.ParseCertificateChain(chain);

        // Assert
        parsed.Leaf.Subject.Should().Contain("device.example.com");
        parsed.Leaf.HasPrivateKey.Should().BeFalse();
        parsed.FullChain.Should().ContainSingle();
        parsed.Issuers.Should().BeEmpty();
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
        var profile = new EstProfile
        {
            Id = Guid.NewGuid(),
            Name = "Test Profile",
            PathPrefix = "/.well-known/est",
            HostnameMatchType = HostnameMatchType.Exact,
            CaBackendId = Guid.NewGuid(),
            ValidityDays = 90,
            RequireClientCertificate = false,
            IsEnabled = true
        };
        profile.Hostnames.Clear();
        profile.Hostnames.Add("test.example.com");
        return profile;
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
