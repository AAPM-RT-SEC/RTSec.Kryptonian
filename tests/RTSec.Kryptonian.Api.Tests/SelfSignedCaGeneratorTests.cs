using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;
using RTSec.Kryptonian.Api.Controllers;
using Xunit;

namespace RTSec.Kryptonian.Api.Tests;

public class SelfSignedCaGeneratorTests
{
    [Fact]
    public void GeneratesPasswordProtectedSigningCaWithoutOverwriting()
    {
        var directory = Directory.CreateTempSubdirectory("kryptonian-ca-test-");
        var path = Path.Combine(directory.FullName, "test.pfx");
        const string password = "test-only-password-123";
        try
        {
            SelfSignedCaGenerator.Generate(path, password, "Test CA, with punctuation");
            var bytes = File.ReadAllBytes(path);
            using var ca = new X509Certificate2(bytes, password, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
            Assert.True(ca.HasPrivateKey);
            Assert.Equal(ca.Subject, ca.Issuer);
            Assert.True(ca.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);
            Assert.Equal(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                ca.Extensions.OfType<X509KeyUsageExtension>().Single().KeyUsages);
            Assert.Throws<CryptographicException>(() => new X509Certificate2(bytes, "wrong-password", X509KeyStorageFlags.EphemeralKeySet));
            using var leafKey = RSA.Create(2048);
            var csr = new CertificateRequest("CN=test-device", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var leaf = csr.Create(ca, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            Assert.Equal(ca.Subject, leaf.Issuer);
            Assert.Throws<IOException>(() => SelfSignedCaGenerator.Generate(path, password, "Replacement"));
            Assert.Equal(bytes, File.ReadAllBytes(path));
            if (OperatingSystem.IsWindows())
                Assert.True(new FileInfo(path).GetAccessControl().AreAccessRulesProtected);
            else
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("relative.pfx", "long-test-password", "CA")]
    [InlineData("C:\\test.pfx", "short", "CA")]
    [InlineData("C:\\test.pfx", "long-test-password", "")]
    [InlineData("\\\\server\\share\\test.pfx", "long-test-password", "CA")]
    [InlineData("C:\\test.txt", "long-test-password", "CA")]
    public void RejectsInvalidInput(string path, string password, string name)
    {
        Assert.Throws<ArgumentException>(() => SelfSignedCaGenerator.Generate(path, password, name));
    }

    [Fact]
    public void GenerationRequiresSystemAdmin()
    {
        var attributes = typeof(CaBackendsController).GetMethod(nameof(CaBackendsController.GenerateSelfSigned))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
        Assert.Equal("SystemAdmin", Assert.Single(attributes).Policy);
    }
}
