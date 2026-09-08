using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RTSec.Kryptonian.Api.Authentication;

namespace RTSec.Kryptonian.Api.Tests.Authentication;

public class ConfiguredClientCertificateTrustTests
{
    [Theory]
    [InlineData("client", true)]
    [InlineData("server-only", false)]
    [InlineData("untrusted", false)]
    [InlineData("no-revocation-evidence", false)]
    public void RequiresClientUsageTrustedIssuerAndRevocationEvidence(string scenario, bool accepted)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=TLS Test CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var issuer = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var pemPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(pemPath, issuer.ExportCertificatePem());
            var trust = new ConfiguredClientCertificateTrust([pemPath]);
            using var leafKey = RSA.Create(2048);
            var csr = new CertificateRequest("CN=client", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            csr.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            csr.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(scenario == "server-only" ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") }, false));
            using var leaf = scenario == "untrusted"
                ? csr.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1))
                : csr.Create(issuer, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            var actual = scenario == "no-revocation-evidence"
                ? trust.Validate(leaf, null)
                : trust.Validate(leaf, null, X509RevocationMode.NoCheck); // isolate issuer/EKU in this unit test
            Assert.Equal(accepted, actual);
        }
        finally { File.Delete(pemPath); }
    }
}
