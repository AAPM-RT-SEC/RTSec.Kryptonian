using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Globalization;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RTSec.Kryptonian.DimseTlsProxy;
using Xunit;

namespace RTSec.Kryptonian.DimseTlsProxy.Tests;

public class DimseTlsProxyWorkerTests
{
    [Fact]
    public void OperationalModeFailsClosedWithoutClientTrust()
    {
        using var server = CreateServerCertificate();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, server.Export(X509ContentType.Pfx, "test"));
            var config = Configuration([("Proxy:ServerCertificatePath", path), ("Proxy:ServerCertificatePassword", "test")]);
            var provider = new ServerCertificateProvider(config, NullLogger<ServerCertificateProvider>.Instance);

            var act = () => new DimseTlsProxyWorker(provider, config, NullLogger<DimseTlsProxyWorker>.Instance);

            act.Should().Throw<InvalidOperationException>().WithMessage("*ClientCaPath*");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task OperationalModeAcceptsConfiguredClientCaWithoutHarnessConfiguration()
    {
        using var root = CreateRoot();
        var crlPort = ReservePort();
        using var crlListener = new HttpListener();
        crlListener.Prefixes.Add($"http://127.0.0.1:{crlPort}/");
        crlListener.Start();
        using var client = CreateClientCertificate(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5),
            $"http://127.0.0.1:{crlPort}/{Guid.NewGuid():N}.crl");
        var serve = ServeCrlAsync(crlListener, CreateCrl(root));
        await using var host = await ProxyTestHost.StartAsync(root);

        var result = await AuthenticateAsync(host.Port, client);
        await Task.Delay(100);
        result.Success.Should().BeTrue($"{result.Failure}; {host.Diagnostics}");
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task OperationalModeRejectsUnknownAndExpiredClientCertificates()
    {
        using var trustedRoot = CreateRoot(DateTimeOffset.UtcNow.AddDays(-3));
        using var unknownRoot = CreateRoot();
        using var unknown = CreateClientCertificate(unknownRoot, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        using var expired = CreateClientCertificate(trustedRoot, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1));
        await using var host = await ProxyTestHost.StartAsync(trustedRoot);

        (await AuthenticateAsync(host.Port, unknown)).Success.Should().BeFalse();
        (await AuthenticateAsync(host.Port, expired)).Success.Should().BeFalse();
    }

    [Fact]
    public async Task OperationalModeRejectsRevokedClientCertificateFromHttpCrl()
    {
        using var root = CreateRoot();
        var crlPort = ReservePort();
        using var crlListener = new HttpListener();
        crlListener.Prefixes.Add($"http://127.0.0.1:{crlPort}/");
        crlListener.Start();
        using var client = CreateClientCertificate(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5),
            $"http://127.0.0.1:{crlPort}/{Guid.NewGuid():N}.crl");
        var crl = CreateCrl(root, client);
        var serve = ServeCrlAsync(crlListener, crl);
        await using var host = await ProxyTestHost.StartAsync(root);

        var result = await AuthenticateAsync(host.Port, client);
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        result.Success.Should().BeFalse();
    }

    private static IConfiguration Configuration(IEnumerable<(string Key, string? Value)> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(value => value.Key, value => value.Value)).Build();

    private static async Task<AuthenticationResult> AuthenticateAsync(int port, X509Certificate2 clientCertificate)
    {
        using var client = new TcpClient();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port);
                break;
            }
            catch (SocketException) when (attempt < 20) { await Task.Delay(25); }
        }

        using var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
        try
        {
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "localhost",
                ClientCertificates = [clientCertificate],
                EnabledSslProtocols = SslProtocols.Tls12
            });
            await ssl.WriteAsync(new byte[] { 0 });
            using var close = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                var read = await ssl.ReadAsync(new byte[1], close.Token);
                return new(read != 0, read == 0 ? "Proxy closed the association" : null);
            }
            catch (OperationCanceledException) { return new(true, null); }
        }
        catch (AuthenticationException ex) { return new(false, $"{ex.Message}; {ex.InnerException?.Message}"); }
        catch (IOException ex) { return new(false, ex.Message); }
    }

    private static X509Certificate2 CreateRoot(DateTimeOffset? notBefore = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Proxy Test Root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var certificate = request.CreateSelfSigned(notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx, "test"), "test", X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 CreateServerCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx, "test"), "test", X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 CreateClientCertificate(X509Certificate2 issuer, DateTimeOffset notBefore, DateTimeOffset notAfter, string? cdp = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=client", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.2") }, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var issuerSki = issuer.Extensions.OfType<X509SubjectKeyIdentifierExtension>().Single();
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(Convert.FromHexString(issuerSki.SubjectKeyIdentifier!)));
        if (cdp is not null) request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([cdp], false));
        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7f;
        using var issued = request.Create(issuer, notBefore, notAfter, serial);
        using var withKey = issued.CopyWithPrivateKey(key);
        return new X509Certificate2(withKey.Export(X509ContentType.Pfx, "test"), "test", X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
    }

    private static byte[] CreateCrl(X509Certificate2 issuer, X509Certificate2? revoked = null)
    {
        var thisUpdate = DateTimeOffset.UtcNow.AddMinutes(-1);
        var builder = new CertificateRevocationListBuilder();
        if (revoked is not null)
            builder.AddEntry(Convert.FromHexString(revoked.SerialNumber), thisUpdate, X509RevocationReason.KeyCompromise);
        return builder.Build(issuer, 1, DateTimeOffset.UtcNow.AddMinutes(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, thisUpdate);
    }

    private static async Task ServeCrlAsync(HttpListener listener, byte[] crl)
    {
        var context = await listener.GetContextAsync();
        context.Response.ContentType = "application/pkix-crl";
        context.Response.ContentLength64 = crl.Length;
        await context.Response.OutputStream.WriteAsync(crl);
        context.Response.Close();
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class ProxyTestHost : IAsyncDisposable
    {
        private readonly string _directory;
        private readonly DimseTlsProxyWorker _worker;
        private readonly TcpListener _upstream;
        private readonly CapturingLogger<DimseTlsProxyWorker> _logger;

        private ProxyTestHost(string directory, DimseTlsProxyWorker worker, TcpListener upstream, int port, CapturingLogger<DimseTlsProxyWorker> logger)
        {
            _directory = directory;
            _worker = worker;
            _upstream = upstream;
            _logger = logger;
            Port = port;
        }

        public int Port { get; }
        public string Diagnostics => string.Join(" | ", _logger.Messages);

        public static async Task<ProxyTestHost> StartAsync(X509Certificate2 root)
        {
            var directory = Path.Combine(Path.GetTempPath(), "KryptonianProxyTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var serverPath = Path.Combine(directory, "server.pfx");
            var caPath = Path.Combine(directory, "client-ca.pem");
            using var server = CreateServerCertificate();
            await File.WriteAllBytesAsync(serverPath, server.Export(X509ContentType.Pfx, "test"));
            await File.WriteAllTextAsync(caPath, root.ExportCertificatePem());
            var upstream = new TcpListener(IPAddress.Loopback, 0);
            upstream.Start();
            var port = ReservePort();
            var config = Configuration([
                ("Proxy:ServerCertificatePath", serverPath), ("Proxy:ServerCertificatePassword", "test"),
                ("Proxy:ClientCaPath", caPath), ("Proxy:ListenAddress", "127.0.0.1"), ("Proxy:ListenPort", port.ToString(CultureInfo.InvariantCulture)),
                ("Proxy:UpstreamHost", "127.0.0.1"), ("Proxy:UpstreamPort", ((IPEndPoint)upstream.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture))]);
            var provider = new ServerCertificateProvider(config, NullLogger<ServerCertificateProvider>.Instance);
            var logger = new CapturingLogger<DimseTlsProxyWorker>();
            var worker = new DimseTlsProxyWorker(provider, config, logger);
            await worker.StartAsync(CancellationToken.None);
            return new ProxyTestHost(directory, worker, upstream, port, logger);
        }

        public async ValueTask DisposeAsync()
        {
            await _worker.StopAsync(CancellationToken.None);
            _upstream.Stop();
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed record AuthenticationResult(bool Success, string? Failure);
}
