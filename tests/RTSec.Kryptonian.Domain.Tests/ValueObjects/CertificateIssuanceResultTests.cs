using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using RTSec.Kryptonian.Domain.ValueObjects;
using Xunit;

namespace RTSec.Kryptonian.Domain.Tests.ValueObjects;

public class CertificateIssuanceResultTests
{
    [Fact]
    public void Successful_CreatesSuccessfulResult()
    {
        // Arrange
        using var cert = CreateTestCertificate();
        var chain = new[] { cert };

        // Act
        var result = CertificateIssuanceResult.Successful(cert, chain);

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.Should().Be(cert);
        result.CertificateChain.Should().BeEquivalentTo(chain);
        result.ErrorMessage.Should().BeNull();
        result.IsPending.Should().BeFalse();
        result.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public void Failed_CreatesFailedResult()
    {
        // Act
        var result = CertificateIssuanceResult.Failed("Connection timeout");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Connection timeout");
        result.Certificate.Should().BeNull();
        result.CertificateChain.Should().BeNull();
        result.IsPending.Should().BeFalse();
        result.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public void Pending_CreatesPendingResult()
    {
        // Act
        var result = CertificateIssuanceResult.Pending(30);

        // Assert
        result.Success.Should().BeFalse();
        result.IsPending.Should().BeTrue();
        result.RetryAfterSeconds.Should().Be(30);
        result.ErrorMessage.Should().BeNull();
        result.Certificate.Should().BeNull();
        result.CertificateChain.Should().BeNull();
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=Test"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5));

        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx, "test"),
            "test",
            X509KeyStorageFlags.Exportable);
    }
}
