using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Kryptonian.MedicalDevice.Enrollment.Tests;

public class GatewayTrustTests
{
    [Fact]
    public async Task ExplicitCaValidatesLiveHttpsAndStillRejectsWrongIdentityAndExpiry()
    {
        using var key = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Private test root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(2));
        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        serverRequest.CertificateExtensions.Add(san.Build());
        using var issued = serverRequest.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(16));
        using var attached = issued.CopyWithPrivateKey(serverKey);
        using var serverCertificate = X509CertificateLoader.LoadPkcs12(attached.Export(X509ContentType.Pfx, "test"), "test", X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
        using var expired = serverRequest.Create(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddHours(-1), RandomNumberGenerator.GetBytes(16));
        var directory = Directory.CreateTempSubdirectory("gateway-trust-test-");
        try
        {
            var path = Path.Combine(directory.FullName, "ca.pem");
            File.WriteAllText(path, root.ExportCertificatePem());
            var strict = GatewayTrust.FromFile(path);
            Assert.False(strict.SkipServerRevocation);
            Assert.False(strict.Validate(issued, null, SslPolicyErrors.RemoteCertificateChainErrors));
            var trust = strict with { SkipServerRevocation = true };
            trust = System.Text.Json.JsonSerializer.Deserialize<GatewayTrust>(System.Text.Json.JsonSerializer.Serialize(trust))!;
            Assert.True(trust.Validate(issued, null, SslPolicyErrors.RemoteCertificateChainErrors));
            Assert.False(trust.Validate(issued, null, SslPolicyErrors.RemoteCertificateNameMismatch));
            Assert.False(trust.Validate(expired, null, SslPolicyErrors.None));
            using var otherKey = RSA.Create(2048);
            var otherRequest = new CertificateRequest("CN=Other root", otherKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            otherRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            using var otherRoot = otherRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            Assert.False(new GatewayTrust(otherRoot.RawData, true).Validate(issued, null, SslPolicyErrors.None));
            File.WriteAllText(path, issued.ExportCertificatePem());
            Assert.Throws<ArgumentException>(() => GatewayTrust.FromFile(path));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serve = Task.Run(async () =>
            {
                using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
                using var tls = new SslStream(connection.GetStream());
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = serverCertificate }, timeout.Token);
                using var reader = new StreamReader(tls, leaveOpen: true);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
                await tls.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK"), timeout.Token);
            }, timeout.Token);
            using var http = GatewayTrust.CreateClient(trust);
            Assert.Equal("OK", await http.GetStringAsync($"https://localhost:{port}/", timeout.Token));
            await serve;
        }
        finally { directory.Delete(true); }
    }
}
