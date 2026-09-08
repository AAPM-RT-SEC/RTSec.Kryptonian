using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RTSec.Kryptonian.Application.Services;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Application.Tests.Services;

public class EnrollmentCertificatePolicyTests
{
    private static EstProfile Profile()
    {
        var profile = new EstProfile { ValidityDays = 30 };
        profile.AllowedKeyUsages.AddRange(["digitalSignature", "clientAuth", "serverAuth"]);
        return profile;
    }

    private static CertificateRequest Request(RSA key, string name = "scanner") =>
        new($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    private static ParsedCsr Csr(CertificateRequest request) => new() { RawData = request.CreateSigningRequest() };

    [Theory]
    [InlineData(1024, null, false)]
    [InlineData(2048, "another-device", false)]
    [InlineData(2048, "scanner", true)]
    [InlineData(2048, null, true)]
    public void InitialRequestIsLimitedToApprovedNameAndStrongKey(int bits, string? san, bool accepted)
    {
        using var key = RSA.Create(bits);
        var request = Request(key);
        if (san != null)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(san);
            request.CertificateExtensions.Add(names.Build());
        }
        var error = Record.Exception(() => EnrollmentCertificatePolicy.ValidateRequest(Csr(request), "scanner", Profile()));
        Assert.Equal(accepted, error == null);
    }

    [Fact]
    public void CaRequestsAndUnspecifiedEkusAreRejected()
    {
        using var key = RSA.Create(2048);
        var request = Request(key);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        Assert.Throws<CryptographicException>(() => EnrollmentCertificatePolicy.ValidateRequest(Csr(request), "scanner", Profile()));
        Assert.Throws<CryptographicException>(() => EnrollmentCertificatePolicy.ValidateRequest(Csr(Request(key)), "scanner", new EstProfile()));
    }

    [Fact]
    public void RenewalRejectsSubjectAndSanChanges()
    {
        using var key = RSA.Create(2048);
        var original = Request(key);
        using var existing = original.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var changed = new CertificateRequest("CN=scanner,O=New organization", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.Throws<CryptographicException>(() => EnrollmentCertificatePolicy.ValidateRequest(Csr(changed), "scanner", Profile(), existing));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("scanner");
        original.CertificateExtensions.Add(san.Build());
        Assert.Throws<CryptographicException>(() => EnrollmentCertificatePolicy.ValidateRequest(Csr(original), "scanner", Profile(), existing));
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("wrong-key", false)]
    [InlineData("wrong-issuer", false)]
    [InlineData("wrong-name", false)]
    [InlineData("wrong-eku", false)]
    [InlineData("too-long", false)]
    [InlineData("ca", false)]
    public void ReturnedCertificateMustMatchAuthorizedRequestAndIssuer(string scenario, bool accepted)
    {
        using var caKey = RSA.Create(2048);
        var caRequest = Request(caKey, "Policy Test CA");
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
        using var key = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        var request = Request(key);
        var issuedRequest = Request(scenario == "wrong-key" ? otherKey : key, scenario == "wrong-name" ? "another" : "scanner");
        issuedRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(scenario == "ca", false, 0, true));
        issuedRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var ekus = new OidCollection { new("1.3.6.1.5.5.7.3.2"), new(scenario == "wrong-eku" ? "1.3.6.1.5.5.7.3.3" : "1.3.6.1.5.5.7.3.1") };
        issuedRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(ekus, false));
        using var leaf = issuedRequest.Create(ca, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(scenario == "too-long" ? 31 : 30), RandomNumberGenerator.GetBytes(16));
        var error = Record.Exception(() => EnrollmentCertificatePolicy.ValidateIssued(request, Profile(), leaf, [leaf, ca], scenario == "wrong-issuer" ? [] : [ca]));
        Assert.Equal(accepted, error == null);
    }
}
