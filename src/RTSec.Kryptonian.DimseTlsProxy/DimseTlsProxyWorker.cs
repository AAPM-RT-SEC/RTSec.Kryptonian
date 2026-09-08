using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RTSec.Kryptonian.DimseTlsProxy;

public sealed class DimseTlsProxyWorker : BackgroundService
{
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AssociationLifetime = TimeSpan.FromMinutes(5);

    private readonly ServerCertificateProvider _serverCert;
    private readonly CaHarnessClient? _caHarness;
    private readonly IConfiguration _config;
    private readonly ILogger<DimseTlsProxyWorker> _logger;
    private readonly X509Certificate2Collection _trustedClientCas = [];
    private readonly bool _harnessMode;

    public DimseTlsProxyWorker(
        ServerCertificateProvider serverCert,
        IConfiguration config,
        ILogger<DimseTlsProxyWorker> logger,
        CaHarnessClient? caHarness = null)
    {
        _serverCert = serverCert ?? throw new ArgumentNullException(nameof(serverCert));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _harnessMode = _config.GetValue<bool>("Proxy:HarnessMode");
        _caHarness = caHarness;

        if (_harnessMode)
        {
            if (_caHarness is null) throw new InvalidOperationException("Proxy:HarnessMode requires the CA harness client.");
            return;
        }

        var path = _config["Proxy:ClientCaPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("Proxy:ClientCaPath must name an existing PEM trust bundle.");

        _trustedClientCas.ImportFromPemFile(path);
        if (_trustedClientCas.Count == 0 || _trustedClientCas.Cast<X509Certificate2>().Any(cert => !IsCertificateAuthority(cert)))
            throw new InvalidOperationException("Proxy:ClientCaPath must contain only CA certificates.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listenPort = _config.GetValue<int?>("Proxy:ListenPort") ?? (int.TryParse(_config["LISTEN_PORT"], out var legacyListenPort) ? legacyListenPort : 4243);
        var upstreamHost = _config["Proxy:UpstreamHost"] ?? _config["ORTHANC_HOST"] ?? IPAddress.Loopback.ToString();
        var upstreamPort = _config.GetValue<int?>("Proxy:UpstreamPort") ?? (int.TryParse(_config["ORTHANC_PORT"], out var legacyUpstreamPort) ? legacyUpstreamPort : 4242);
        var listenAddress = ParseListenAddress(_config["Proxy:ListenAddress"]);

        using var listener = new TcpListener(listenAddress, listenPort);
        listener.Start();
        _logger.LogInformation("DIMSE TLS proxy listening on {Address}:{Port}, forwarding to {Host}:{ForwardPort}",
            listenAddress, listenPort, upstreamHost, upstreamPort);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(stoppingToken);
                    _ = HandleConnectionAsync(client, upstreamHost, upstreamPort, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }
        }
        finally { listener.Stop(); }
    }

    private async Task HandleConnectionAsync(TcpClient client, string upstreamHost, int upstreamPort, CancellationToken stoppingToken)
    {
        var remote = client.Client.RemoteEndPoint;
        using (client)
        {
        using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, ValidatePresentedClientCertificate);
        X509Certificate2? clientCertificate = null;
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            handshake.CancelAfter(HandshakeTimeout);
            using var serverCertificate = _serverCert.LoadCertificate();
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = serverCertificate,
                ClientCertificateRequired = true,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, handshake.Token);
            clientCertificate = ssl.RemoteCertificate is null ? null : new X509Certificate2(ssl.RemoteCertificate);
            if (clientCertificate is null) return;

            if (_harnessMode && !await VerifyHarnessClientAsync(clientCertificate, stoppingToken)) return;

            using var association = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            association.CancelAfter(GetAssociationLifetime(clientCertificate, serverCertificate));
            using var upstream = new TcpClient();
            await upstream.ConnectAsync(upstreamHost, upstreamPort, association.Token);
            await CopyAssociationAsync(ssl, upstream.GetStream(), association);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning("TLS proxy connection from {Remote} failed: {Message}", remote, ex.Message);
        }
        finally { clientCertificate?.Dispose(); }
        }
    }

    private bool ValidatePresentedClientCertificate(object _, X509Certificate? certificate, X509Chain? __, SslPolicyErrors ___)
    {
        if (certificate is null) return false;
        if (_harnessMode) return true;

        using var client = new X509Certificate2(certificate);
        if (IsCertificateAuthority(client) || !HasClientAuth(client)) return false;
        var now = DateTime.UtcNow;
        if (now < client.NotBefore.ToUniversalTime() || now > client.NotAfter.ToUniversalTime()) return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.DisableCertificateDownloads = false;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ClientAuthOid));
        chain.ChainPolicy.CustomTrustStore.AddRange(_trustedClientCas);
        if (chain.Build(client)) return true;
        _logger.LogWarning("Client certificate chain validation failed: {Status}",
            string.Join("; ", chain.ChainStatus.Select(status => status.Status)));
        return false;
    }

    private async Task<bool> VerifyHarnessClientAsync(X509Certificate2 clientCertificate, CancellationToken ct)
    {
        var verification = await _caHarness!.VerifyCertAsync(Convert.ToBase64String(clientCertificate.RawData), ct);
        if (verification is null) return false;
        _ = _caHarness.RecordCStoreEventAsync(verification.TeamToken, verification.Backend,
            verification.SerialNumber, verification.Thumbprint, ct);
        return true;
    }

    private static async Task CopyAssociationAsync(SslStream client, NetworkStream upstream, CancellationTokenSource association)
    {
        var clientToUpstream = client.CopyToAsync(upstream, association.Token);
        var upstreamToClient = upstream.CopyToAsync(client, association.Token);
        await Task.WhenAny(clientToUpstream, upstreamToClient);
        association.Cancel();
        try { await Task.WhenAll(clientToUpstream, upstreamToClient); }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private static TimeSpan GetAssociationLifetime(X509Certificate2 clientCertificate, X509Certificate2 serverCertificate)
    {
        var expiry = clientCertificate.NotAfter < serverCertificate.NotAfter
            ? clientCertificate.NotAfter.ToUniversalTime()
            : serverCertificate.NotAfter.ToUniversalTime();
        var untilExpiry = expiry - DateTime.UtcNow;
        return untilExpiry <= TimeSpan.Zero ? TimeSpan.Zero : untilExpiry < AssociationLifetime ? untilExpiry : AssociationLifetime;
    }

    private static bool HasClientAuth(X509Certificate2 certificate) => certificate.Extensions
        .OfType<X509EnhancedKeyUsageExtension>()
        .Any(extension => extension.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == ClientAuthOid));

    private static bool IsCertificateAuthority(X509Certificate2 certificate) => certificate.Extensions
        .OfType<X509BasicConstraintsExtension>().Any(extension => extension.CertificateAuthority);

    private static IPAddress ParseListenAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return IPAddress.Loopback;
        return IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException("Proxy:ListenAddress must be an IP address.");
    }

    public override void Dispose()
    {
        base.Dispose();
        foreach (var certificate in _trustedClientCas) certificate.Dispose();
        _trustedClientCas.Clear();
    }
}
