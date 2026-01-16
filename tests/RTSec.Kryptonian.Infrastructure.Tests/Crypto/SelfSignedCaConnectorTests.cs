using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.ValueObjects;
using RTSec.Kryptonian.Infrastructure.Crypto;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Crypto;

public class NotWindowsFactAttribute : FactAttribute
{
    public NotWindowsFactAttribute()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "This test does not on Windows.";
        }
    }
}
public class SelfSignedCaConnectorTests : IDisposable
{
    private readonly Mock<ILogger<SelfSignedCaConnector>> _loggerMock;
    private readonly X509Certificate2 _caCertificate;
    private readonly SelfSignedCaConnector _sut;

    // Skip tests on Windows due to CNG private key export limitations
    public SelfSignedCaConnectorTests()
    {
        _loggerMock = new Mock<ILogger<SelfSignedCaConnector>>();



        _caCertificate = CreateCaCertificate();
        _sut = new SelfSignedCaConnector(_loggerMock.Object, _caCertificate);
    }

    public void Dispose()
    {
        _caCertificate?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void ConstructorWithValidCaCertSetsTypeToSelfSigned()
    {
        // Assert
        _sut.Type.Should().Be(CaBackendType.SelfSigned);
    }

    [Fact]
    public void ConstructorWithNullCaCertThrowsArgumentNullException()
    {
        // Act
        var act = () => new SelfSignedCaConnector(_loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConstructorWithCertWithoutPrivateKeyThrowsArgumentException()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var certWithoutKey = CreateCertWithoutPrivateKey(rsa);

        // Act
        var act = () => new SelfSignedCaConnector(_loggerMock.Object, certWithoutKey);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*private key*");
        certWithoutKey.Dispose();
    }

    #endregion

    #region GetCaCertificatesAsync Tests


    [NotWindowsFact]
    public async Task GetCaCertificatesAsyncReturnsCaCertificate()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Act
        var result = await _sut.GetCaCertificatesAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Subject.Should().Be(_caCertificate.Subject);
    }

    #endregion

    #region IssueCertificateAsync Tests

    [NotWindowsFact]
    public async Task IssueCertificateAsyncWithValidCsrReturnsSuccessfulResult()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var parsedCsr = CreateParsedCsr("CN=TestDevice");
        var profile = CreateEstProfile();

        // Act
        var result = await _sut.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.Should().NotBeNull();
        result.Certificate!.Subject.Should().Contain("CN=TestDevice");
        result.CertificateChain.Should().HaveCount(2);
    }

    [NotWindowsFact]
    public async Task IssueCertificateAsyncSetsCorrectValidityPeriod()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var parsedCsr = CreateParsedCsr("CN=ValidityTest");
        var profile = CreateEstProfile(validityDays: 30);

        // Act
        var result = await _sut.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        var expectedNotAfter = DateTime.UtcNow.AddDays(30);
        // Certificate NotAfter is in local time, convert to UTC for comparison
        result.Certificate!.NotAfter.ToUniversalTime()
            .Should().BeCloseTo(expectedNotAfter, TimeSpan.FromMinutes(10));
    }

    [NotWindowsFact]
    public async Task IssueCertificateAsyncSetsBasicConstraintsToNotCa()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var parsedCsr = CreateParsedCsr("CN=BasicConstraintsTest");
        var profile = CreateEstProfile();

        // Act
        var result = await _sut.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        var basicConstraints = result.Certificate!.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .FirstOrDefault();
        basicConstraints.Should().NotBeNull();
        basicConstraints!.CertificateAuthority.Should().BeFalse();
    }

    [NotWindowsFact]
    public async Task IssueCertificateAsyncSetsKeyUsageFromProfile()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var parsedCsr = CreateParsedCsr("CN=KeyUsageTest");
        var profile = CreateEstProfile(allowedKeyUsages: new List<string> { "DigitalSignature", "KeyEncipherment" });

        // Act
        var result = await _sut.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        var keyUsage = result.Certificate!.Extensions
            .OfType<X509KeyUsageExtension>()
            .FirstOrDefault();
        keyUsage.Should().NotBeNull();
        keyUsage!.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.DigitalSignature);
        keyUsage.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.KeyEncipherment);
    }

    [NotWindowsFact]
    public async Task IssueCertificateAsyncSetsAuthorityKeyIdentifier()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var parsedCsr = CreateParsedCsr("CN=AkiTest");
        var profile = CreateEstProfile();

        // Act
        var result = await _sut.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        // AKI OID: 2.5.29.35
        var aki = result.Certificate!.Extensions
            .Cast<X509Extension>()
            .FirstOrDefault(e => e.Oid?.Value == "2.5.29.35");
        aki.Should().NotBeNull();
    }

    [NotWindowsFact]
    public async Task IssueCertificateAsyncSetsSubjectKeyIdentifier()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");

        // Arrange
        var parsedCsr = CreateParsedCsr("CN=SkiTest");
        var profile = CreateEstProfile();

        // Act
        var result = await _sut.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        var ski = result.Certificate!.Extensions
            .OfType<X509SubjectKeyIdentifierExtension>()
            .FirstOrDefault();
        ski.Should().NotBeNull();
    }

    [NotWindowsFact]
    public async Task IssueCertificateAsyncWithNullCsrThrowsArgumentNullException()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var profile = CreateEstProfile();

        // Act
        var act = () => _sut.IssueCertificateAsync(null!, profile);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [NotWindowsFact]
    public async Task IssueCertificateAsyncWithNullProfileThrowsArgumentNullException()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var parsedCsr = CreateParsedCsr("CN=Test");

        // Act
        var act = () => _sut.IssueCertificateAsync(parsedCsr, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region TestConnectionAsync Tests

    [NotWindowsFact]
    public async Task TestConnectionAsyncWithValidCaReturnsTrue()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");

        // Act
        var result = await _sut.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [NotWindowsFact]
    public async Task TestConnectionAsyncHandlesTimezoneConversionCorrectly()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // This test verifies the fix for the timezone bug where:
        // - X509Certificate2.NotBefore/NotAfter return Local time
        // - Comparison with DateTime.UtcNow was failing
        // - Fix: Use .ToUniversalTime() on certificate properties

        // The certificate is created in the constructor with valid times
        // This test ensures the method handles timezone conversion properly
        var result = await _sut.TestConnectionAsync();

        // Should pass regardless of local timezone offset
        result.Should().BeTrue();
    }
    #endregion

    #region RevokeCertificateAsync Tests

    [NotWindowsFact]
    public async Task RevokeCertificateAsyncReturnsFalse()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Revocation not implemented for self-signed CA

        // Act
        var result = await _sut.RevokeCertificateAsync("ABC123", RevocationReason.Unspecified);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Certificate Chain Tests

    [NotWindowsFact]
    public async Task IssueCertificateAsyncCertificateChainHasCorrectOrder()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var parsedCsr = CreateParsedCsr("CN=ChainOrderTest");
        var profile = CreateEstProfile();

        // Act
        var result = await _sut.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        result.CertificateChain.Should().HaveCount(2);

        // First cert should be end-entity (subject matches CSR)
        result.CertificateChain![0].Subject.Should().Contain("CN=ChainOrderTest");

        // Second cert should be CA (issuer of first cert)
        result.CertificateChain[1].Subject.Should().Be(_caCertificate.Subject);
    }

    [NotWindowsFact]
    public async Task IssueCertificateAsyncEndEntityIssuerMatchesCaSubject()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");


        // Arrange
        var parsedCsr = CreateParsedCsr("CN=IssuerMatchTest");
        var profile = CreateEstProfile();

        // Act
        var result = await _sut.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate!.Issuer.Should().Be(_caCertificate.Subject);
    }

    #endregion

    #region ECDSA CA Tests

    [NotWindowsFact]
    public async Task IssueCertificateAsyncWithEcdsaCaIssuesValidCertificate()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Extracting Private Keys not allowed on MS Windows");

        // Arrange
        using var ecdsaCa = CreateEcdsaCaCertificate();
        var ecdsaConnector = new SelfSignedCaConnector(_loggerMock.Object, ecdsaCa);
        var parsedCsr = CreateParsedCsr("CN=EcdsaTest");
        var profile = CreateEstProfile();

        // Act
        var result = await ecdsaConnector.IssueCertificateAsync(parsedCsr, profile);

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static X509Certificate2 CreateCaCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=Test CA, O=Test Org"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 1, true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(10));

        // Export and reimport to make key exportable
        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx, "test"),
            "test",
            X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 CreateEcdsaCaCertificate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=Test ECDSA CA"),
            ecdsa,
            HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 1, true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(10));

        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx, "test"),
            "test",
            X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 CreateCertWithoutPrivateKey(RSA rsa)
    {
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=NoPK"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1));

        // Export only the public portion
        return new X509Certificate2(cert.RawData);
    }

    private static ParsedCsr CreateParsedCsr(string subject)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName(subject),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var csrDer = request.CreateSigningRequest();

        return new ParsedCsr
        {
            RawData = csrDer,
            SubjectDn = subject,
            PublicKey = rsa,
            PublicKeyAlgorithm = "RSA",
            KeySize = 2048,
            SignatureAlgorithm = "1.2.840.113549.1.1.11" // SHA256WithRSA
        };
    }

    private static EstProfile CreateEstProfile(
        int validityDays = 365,
        List<string>? allowedKeyUsages = null)
    {
        var profile = new EstProfile
        {
            Id = Guid.NewGuid(),
            Name = "Test Profile",
            PathPrefix = "/.well-known/est",
            ValidityDays = validityDays,
            IsEnabled = true
        };
        profile.Hostnames.Clear();
        profile.Hostnames.Add("test.example.com");
        profile.AllowedKeyUsages.Clear();
        if (allowedKeyUsages != null)
        {
            foreach (var usage in allowedKeyUsages)
            {
                profile.AllowedKeyUsages.Add(usage);
            }
        }
        return profile;
    }

    #endregion

    private static void SkipOnWindows()
    {
        // Skip on Windows due to CNG private key export limitations
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }
}
