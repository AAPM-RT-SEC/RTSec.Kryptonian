using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kryptonian.MedicalDevice.Enrollment;

namespace Kryptonian.MedicalDevice.Enrollment.Tests;

public class EstEnrollmentClientMtlsHandshakeTests
{
    [Fact]
    public async Task EnrolledCertificateCompletesLoopbackMutualTlsAndExchangesBytes()
    {
        using var issuerKey = RSA.Create(2048);
        var issuerRequest = new CertificateRequest("CN=Test CA", issuerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var issuer = issuerRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(2));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var handler = new CsrSigningHandler(issuer);
        using var http = new HttpClient(handler, disposeHandler: false);
        var enrollmentClient = new EstEnrollmentClient(http);

        var enrollment = await enrollmentClient.EnrollAsync(
            new Uri("https://localhost:8443"),
            new DeviceEnrollmentRequest("mtls-device", "RTSec", "Model", "SN-1", "CODE"),
            timeout.Token);
        using var enrolledCertificate = enrollment.Certificate;

        using var serverKey = RSA.Create(2048);
        using var serverCertificate = CreateServerCertificate(serverKey);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = EchoOnceAsync(listener, serverCertificate, enrolledCertificate, timeout.Token);

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            using var tls = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false,
                (_, certificate, _, _) => HasThumbprint(certificate, serverCertificate));
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "localhost",
                ClientCertificates = [enrolledCertificate],
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, timeout.Token);

            await tls.WriteAsync("ping"u8.ToArray(), timeout.Token);
            var reply = new byte[4];
            await tls.ReadExactlyAsync(reply, timeout.Token);

            Assert.Equal("pong"u8.ToArray(), reply);
            Assert.Equal("ping", await server);
            Assert.True(tls.IsMutuallyAuthenticated);
        }
        finally
        {
            timeout.Cancel();
            try { await server; }
            catch (OperationCanceledException) { }
            foreach (var certificate in enrollment.IssuedCertificates) certificate.Dispose();
        }
    }

    private static async Task<string> EchoOnceAsync(
        TcpListener listener, X509Certificate2 serverCertificate, X509Certificate2 expectedClient, CancellationToken ct)
    {
        using var tcp = await listener.AcceptTcpClientAsync(ct);
        using var tls = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false,
            (_, certificate, _, _) => HasThumbprint(certificate, expectedClient));
        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = serverCertificate,
            ClientCertificateRequired = true,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        }, ct);

        var request = new byte[4];
        await tls.ReadExactlyAsync(request, ct);
        await tls.WriteAsync("pong"u8.ToArray(), ct);
        return Encoding.ASCII.GetString(request);
    }

    private static bool HasThumbprint(X509Certificate? certificate, X509Certificate2 expected)
    {
        if (certificate is null) return false;
        using var peer = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        return string.Equals(peer.Thumbprint, expected.Thumbprint, StringComparison.OrdinalIgnoreCase);
    }

    private static X509Certificate2 CreateServerCertificate(RSA key)
    {
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx, password), password,
            OperatingSystem.IsWindows()
                ? X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable
                : X509KeyStorageFlags.EphemeralKeySet);
    }

    private sealed class CsrSigningHandler(X509Certificate2 issuer) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var csr = CertificateRequest.LoadSigningRequest(Convert.FromBase64String(body.Trim()), HashAlgorithmName.SHA256,
                CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions, RSASignaturePadding.Pkcs1);
            csr.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            csr.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            csr.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.2") }, true));
            var serial = RandomNumberGenerator.GetBytes(16);
            serial[0] &= 0x7f;
            using var leaf = csr.Create(issuer, DateTimeOffset.UtcNow.AddMinutes(-1), issuer.NotAfter.ToUniversalTime().AddMinutes(-5), serial);
            var certificates = new X509Certificate2Collection { leaf, issuer };
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Convert.ToBase64String(certificates.Export(X509ContentType.Pkcs7)!))
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs7-mime");
            return response;
        }
    }
}
