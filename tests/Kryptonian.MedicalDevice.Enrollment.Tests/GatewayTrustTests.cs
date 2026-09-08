using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kryptonian.DICOMTls;
using Xunit;

namespace Kryptonian.MedicalDevice.Enrollment.Tests;

public class GatewayTrustTests
{
    [Fact]
    public void ExplicitDeveloperTrustStillRequiresIssuerHostnameAndServerPurpose()
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var server = request.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), new byte[] { 1 });
        Assert.True(GatewayHttpClient.Validate(server, SslPolicyErrors.RemoteCertificateChainErrors, root.RawData));
        Assert.False(GatewayHttpClient.Validate(server, SslPolicyErrors.RemoteCertificateNameMismatch, root.RawData));
        using var unknownKey = RSA.Create(2048);
        using var unknown = new CertificateRequest("CN=Unknown", unknownKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        Assert.False(GatewayHttpClient.Validate(server, SslPolicyErrors.None, unknown.RawData));
        Assert.False(GatewayHttpClient.Validate(root, SslPolicyErrors.None, root.RawData));
        Assert.False(GatewayHttpClient.Validate(null, SslPolicyErrors.RemoteCertificateNotAvailable, root.RawData));
    }
}
