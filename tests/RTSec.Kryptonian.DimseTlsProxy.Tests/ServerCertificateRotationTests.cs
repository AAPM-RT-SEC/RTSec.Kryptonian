using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RTSec.Kryptonian.DimseTlsProxy;
using Xunit;

namespace RTSec.Kryptonian.DimseTlsProxy.Tests;

public class ServerCertificateRotationTests
{
    [Fact]
    public async Task FreshHandshakeUsesReplacedPfxAndInvalidReplacementFailsClosed()
    {
        var directory = Directory.CreateTempSubdirectory("kryptonian-rotation-");
        var path = Path.Combine(directory.FullName, "server.pfx");
        const string password = "synthetic-test-only";
        using var first = CreateCertificate(DateTimeOffset.UtcNow.AddHours(1));
        using var second = CreateCertificate(DateTimeOffset.UtcNow.AddHours(2));
        using var expired = CreateCertificate(DateTimeOffset.UtcNow.AddMinutes(-1));
        try
        {
            File.WriteAllBytes(path, first.Export(X509ContentType.Pfx, password));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Proxy:ServerCertificatePath"] = path,
                ["Proxy:ServerCertificatePassword"] = password
            }).Build();
            var provider = new ServerCertificateProvider(config, NullLogger<ServerCertificateProvider>.Instance);
            Assert.Equal(first.Thumbprint, await HandshakeThumbprint(provider));
            var replacement = Path.Combine(directory.FullName, "replacement.pfx");
            File.WriteAllBytes(replacement, second.Export(X509ContentType.Pfx, password));
            File.Move(replacement, path, overwrite: true);
            Assert.Equal(second.Thumbprint, await HandshakeThumbprint(provider));
            File.WriteAllBytes(replacement, expired.Export(X509ContentType.Pfx, password));
            File.Move(replacement, path, overwrite: true);
            Assert.Throws<InvalidOperationException>(() => provider.LoadCertificate());
        }
        finally { directory.Delete(recursive: true); }
    }

    private static async Task<string> HandshakeThumbprint(ServerCertificateProvider provider)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(async () =>
        {
            using var tcp = await listener.AcceptTcpClientAsync(timeout.Token);
            using var ssl = new SslStream(tcp.GetStream());
            using var certificate = provider.LoadCertificate();
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, timeout.Token);
            await ssl.WriteAsync(new byte[] { 42 }, timeout.Token);
        });
        using var client = new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, timeout.Token);
        // This test isolates server-key loading/rotation, not trust policy (tested by worker tests).
        using var stream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
        await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" }, timeout.Token);
        using var remote = new X509Certificate2(stream.RemoteCertificate!);
        var data = new byte[1];
        await stream.ReadExactlyAsync(data, timeout.Token);
        Assert.Equal(42, data[0]);
        await server;
        return remote.Thumbprint;
    }

    private static X509Certificate2 CreateCertificate(DateTimeOffset expires)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), expires);
    }
}
