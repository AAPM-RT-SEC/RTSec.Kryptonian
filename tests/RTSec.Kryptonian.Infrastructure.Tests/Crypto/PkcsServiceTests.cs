using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Infrastructure.Crypto;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Crypto;

public class PkcsServiceTests
{
    private readonly PkcsService _sut;
    private readonly Mock<ILogger<PkcsService>> _loggerMock;

    public PkcsServiceTests()
    {
        _loggerMock = new Mock<ILogger<PkcsService>>();
        _sut = new PkcsService(_loggerMock.Object);
    }

    #region ParsePkcs10 Tests

    [Fact]
    public void ParsePkcs10WithValidDerCsrReturnsCorrectParsedCsr()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var csr = CreateCsr(rsa, "CN=TestSubject");
        var derBytes = csr.CreateSigningRequest();

        // Act
        var result = _sut.ParsePkcs10(derBytes);

        // Assert
        result.Should().NotBeNull();
        result.SubjectDn.Should().Contain("CN=TestSubject");
        result.PublicKeyAlgorithm.Should().Be("RSA");
        result.KeySize.Should().Be(2048);
        result.RawData.Should().BeEquivalentTo(derBytes);
    }

    [Fact]
    public void ParsePkcs10WithPemFormatCsrReturnsCorrectParsedCsr()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var csr = CreateCsr(rsa, "CN=PemTest");
        var pemBytes = Encoding.ASCII.GetBytes(csr.CreateSigningRequestPem());

        // Act
        var result = _sut.ParsePkcs10(pemBytes);

        // Assert
        result.Should().NotBeNull();
        result.SubjectDn.Should().Contain("CN=PemTest");
        result.PublicKeyAlgorithm.Should().Be("RSA");
    }

    [Fact]
    public void ParsePkcs10WithBase64EncodedCsrReturnsCorrectParsedCsr()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var csr = CreateCsr(rsa, "CN=Base64Test");
        var derBytes = csr.CreateSigningRequest();
        var base64Bytes = Encoding.ASCII.GetBytes(Convert.ToBase64String(derBytes));

        // Act
        var result = _sut.ParsePkcs10(base64Bytes);

        // Assert
        result.Should().NotBeNull();
        result.SubjectDn.Should().Contain("CN=Base64Test");
    }

    [Fact]
    public void ParsePkcs10WithEcdsaCsrReturnsCorrectKeyInfo()
    {
        // Arrange
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var csr = CreateEcdsaCsr(ecdsa, "CN=EcdsaTest");
        var derBytes = csr.CreateSigningRequest();

        // Act
        var result = _sut.ParsePkcs10(derBytes);

        // Assert
        result.Should().NotBeNull();
        result.PublicKeyAlgorithm.Should().Be("ECDSA");
        result.KeySize.Should().Be(256);
    }

    [Fact]
    public void ParsePkcs10WithNullBytesThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.ParsePkcs10(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParsePkcs10WithEmptyBytesThrowsArgumentException()
    {
        // Act
        var act = () => _sut.ParsePkcs10(Array.Empty<byte>());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region DecodeEstRequestBodyAsync Tests

    [Fact]
    public async Task DecodeEstRequestBodyAsyncWithBase64EncodingDecodesCorrectly()
    {
        // Arrange
        var originalData = new byte[] { 0x30, 0x82, 0x01, 0x00 };
        var base64Data = Convert.ToBase64String(originalData);
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(base64Data));

        // Act
        var result = await _sut.DecodeEstRequestBodyAsync(stream, "base64");

        // Assert
        result.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public async Task DecodeEstRequestBodyAsyncWithNullEncodingAutoDetectsBase64()
    {
        // Arrange
        var originalData = new byte[] { 0x30, 0x82, 0x01, 0x00 };
        var base64Data = Convert.ToBase64String(originalData);
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(base64Data));

        // Act
        var result = await _sut.DecodeEstRequestBodyAsync(stream, null);

        // Assert
        result.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public async Task DecodeEstRequestBodyAsyncWithDerDataReturnsAsIs()
    {
        // Arrange
        var derData = new byte[] { 0x30, 0x82, 0x01, 0x00, 0x55 }; // Starts with SEQUENCE tag
        using var stream = new MemoryStream(derData);

        // Act
        var result = await _sut.DecodeEstRequestBodyAsync(stream, null);

        // Assert
        result.Should().BeEquivalentTo(derData);
    }

    [Fact]
    public async Task DecodeEstRequestBodyAsyncWithNullStreamThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.DecodeEstRequestBodyAsync(null!, "base64");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DecodeEstRequestBodyAsyncExceedingMaxSizeThrowsInvalidOperationException()
    {
        // Arrange
        var largeData = new byte[1000]; // Create 1KB of data
        Array.Fill(largeData, (byte)'A');
        using var stream = new MemoryStream(largeData);

        // Act - Set max size to 500 bytes
        var act = () => _sut.DecodeEstRequestBodyAsync(stream, null, maxSize: 500);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum*500*");
    }

    [Fact]
    public async Task DecodeEstRequestBodyAsyncWithCancellationTokenCanBeCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var stream = new MemoryStream(new byte[] { 0x30 });

        // Act
        var act = () => _sut.DecodeEstRequestBodyAsync(stream, null, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region EncodeToPkcs7 Tests

    [Fact]
    public void EncodeToPkcs7WithSingleCertificateReturnsValidPkcs7()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var cert = CreateSelfSignedCert(rsa, "CN=TestCert");

        // Act
        var result = _sut.EncodeToPkcs7(new[] { cert });

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        // PKCS#7 ContentInfo starts with SEQUENCE tag
        result[0].Should().Be(0x30);
    }

    [Fact]
    public void EncodeToPkcs7WithMultipleCertificatesReturnsValidPkcs7()
    {
        // Arrange
        using var rsa1 = RSA.Create(2048);
        using var rsa2 = RSA.Create(2048);
        var cert1 = CreateSelfSignedCert(rsa1, "CN=Cert1");
        var cert2 = CreateSelfSignedCert(rsa2, "CN=Cert2");

        // Act
        var result = _sut.EncodeToPkcs7(new[] { cert1, cert2 });

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void EncodeToPkcs7WithNullArrayThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EncodeToPkcs7(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EncodeToPkcs7WithEmptyArrayThrowsArgumentException()
    {
        // Act
        var act = () => _sut.EncodeToPkcs7(Array.Empty<X509Certificate2>());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region EncodeEstResponseBody Tests

    [Fact]
    public void EncodeEstResponseBodyEncodesAsBase64()
    {
        // Arrange
        var pkcs7Data = new byte[] { 0x30, 0x82, 0x01, 0x00 };

        // Act
        var result = _sut.EncodeEstResponseBody(pkcs7Data);

        // Assert
        var resultString = Encoding.ASCII.GetString(result);
        var decoded = Convert.FromBase64String(resultString);
        decoded.Should().BeEquivalentTo(pkcs7Data);
    }

    [Fact]
    public void EncodeEstResponseBodyWithNullDataThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EncodeEstResponseBody(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ExportToPem Tests

    [Fact]
    public void ExportToPemReturnsValidPemFormat()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var cert = CreateSelfSignedCert(rsa, "CN=PemExportTest");

        // Act
        var result = _sut.ExportToPem(cert);

        // Assert
        result.Should().StartWith("-----BEGIN CERTIFICATE-----");
        result.Should().EndWith("-----END CERTIFICATE-----" + Environment.NewLine);
    }

    [Fact]
    public void ExportToPemWithNullCertThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.ExportToPem(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ValidateCsrSignature Tests

    [Fact]
    public void ValidateCsrSignatureWithValidSignatureReturnsTrue()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var csr = CreateCsr(rsa, "CN=ValidSig");
        var parsedCsr = _sut.ParsePkcs10(csr.CreateSigningRequest());

        // Act
        var result = _sut.ValidateCsrSignature(parsedCsr);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateCsrSignatureWithNullCsrThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.ValidateCsrSignature(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void RoundTripCsrToPkcs7PreservesIntegrity()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var csr = CreateCsr(rsa, "CN=RoundTrip");
        var csrDer = csr.CreateSigningRequest();

        // Parse CSR
        var parsedCsr = _sut.ParsePkcs10(csrDer);

        // Create a certificate from the CSR (self-signed for testing)
        var cert = CreateSelfSignedCert(rsa, "CN=RoundTrip");

        // Act - Encode to PKCS#7 then to EST format
        var pkcs7 = _sut.EncodeToPkcs7(new[] { cert });
        var estResponse = _sut.EncodeEstResponseBody(pkcs7);

        // Assert
        parsedCsr.SubjectDn.Should().Contain("CN=RoundTrip");
        estResponse.Should().NotBeEmpty();

        // Verify we can decode the response
        var decodedPkcs7 = Convert.FromBase64String(Encoding.ASCII.GetString(estResponse));
        decodedPkcs7.Should().BeEquivalentTo(pkcs7);
    }

    #endregion

    #region Helper Methods

    private static CertificateRequest CreateCsr(RSA rsa, string subject)
    {
        return new CertificateRequest(
            new X500DistinguishedName(subject),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    private static CertificateRequest CreateEcdsaCsr(ECDsa ecdsa, string subject)
    {
        return new CertificateRequest(
            new X500DistinguishedName(subject),
            ecdsa,
            HashAlgorithmName.SHA256);
    }

    private static X509Certificate2 CreateSelfSignedCert(RSA rsa, string subject)
    {
        var request = new CertificateRequest(
            new X500DistinguishedName(subject),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(365));
    }

    #endregion
}
