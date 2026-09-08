using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Kryptonian.MedicalDevice.Enrollment;

namespace Kryptonian.MedicalDevice.Enrollment.Tests;

/// <summary>
/// Renewal CSRs must keep the current certificate's full identity (subject and SANs) while
/// rotating to a new private key. Dropping SANs would break DICOM TLS peers that validate
/// against DNS or IP names rather than the common name.
/// </summary>
public class EstEnrollmentClientRenewalTests
{
    [Fact]
    public void RenewalCsrPreservesSubjectAndSansAndRotatesKey()
    {
        // Arrange: existing device cert with a multi-valued subject and DNS + IP SANs.
        using var oldKey = RSA.Create(2048);
        var subject = new X500DistinguishedName("CN=scanner-7,OU=Radiology,O=Hospital,C=US");

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("scanner-7.hospital.local");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);

        var request = new CertificateRequest(subject, oldKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(sanBuilder.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));

        using var existing = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        using var newKey = RSA.Create(2048);

        // Act
        var csrDer = EstEnrollmentClient.BuildRenewalCsr(newKey, existing, existing.SubjectName);

        // Assert: .NET's own PKCS#10 reader proves the CSR is well formed and signed by the
        // NEW key; UnsafeLoadCertificateExtensions surfaces the carried-over extensions.
        var loaded = CertificateRequest.LoadSigningRequest(
            csrDer,
            HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions,
            RSASignaturePadding.Pkcs1);

        loaded.SubjectName.Format(true).Should().Contain("CN=scanner-7");
        loaded.SubjectName.Format(true).Should().Contain("OU=Radiology");
        loaded.SubjectName.Format(true).Should().Contain("O=Hospital");
        loaded.SubjectName.Format(true).Should().Contain("C=US");

        var san = loaded.CertificateExtensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .SingleOrDefault();
        san.Should().NotBeNull("renewal must carry the existing SAN set, not just the CN");
        san!.EnumerateDnsNames().Should().Contain("scanner-7.hospital.local");
        san.EnumerateIPAddresses().Should().Contain(System.Net.IPAddress.Loopback);

        // Assert: key rotation - the CSR public key is the new one, not the old one.
        using var csrPublic = loaded.PublicKey.GetRSAPublicKey();
        using var oldPublic = RSA.Create(oldKey.ExportParameters(false));
        csrPublic!.ExportRSAPublicKeyPem()
            .Should().NotBe(oldPublic.ExportRSAPublicKeyPem());
    }

    [Fact]
    public void RenewalCsrWithoutSanDoesNotFabricateOne()
    {
        using var oldKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=no-san", oldKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using var existing = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        using var newKey = RSA.Create(2048);
        var csrDer = EstEnrollmentClient.BuildRenewalCsr(newKey, existing, existing.SubjectName);

        var loaded = CertificateRequest.LoadSigningRequest(
            csrDer,
            HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions,
            RSASignaturePadding.Pkcs1);

        loaded.CertificateExtensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .Should().BeEmpty();
    }
}
