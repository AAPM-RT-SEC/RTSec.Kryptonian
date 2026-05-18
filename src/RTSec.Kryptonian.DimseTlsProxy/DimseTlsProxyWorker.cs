using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace RTSec.Kryptonian.DimseTlsProxy;

// Listens on port 4243 for DICOM connections with mTLS.
// Validates the client certificate against the ca-harness, records the scoring event,
// then proxies the raw DIMSE byte stream to Orthanc on port 4242 (plain TCP).
public sealed class DimseTlsProxyWorker : BackgroundService
{
    private readonly ServerCertificateProvider _serverCert;
    private readonly CaHarnessClient _caHarness;
    private readonly IConfiguration _config;
    private readonly ILogger<DimseTlsProxyWorker> _logger;

    public DimseTlsProxyWorker(
        ServerCertificateProvider serverCert,
        CaHarnessClient caHarness,
        IConfiguration config,
        ILogger<DimseTlsProxyWorker> logger)
    {
        _serverCert = serverCert;
        _caHarness = caHarness;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listenPort = int.Parse(_config["LISTEN_PORT"] ?? "4243");
        var orthancHost = _config["ORTHANC_HOST"] ?? "orthanc";
        var orthancPort = int.Parse(_config["ORTHANC_PORT"] ?? "4242");

        var listener = new TcpListener(IPAddress.Any, listenPort);
        listener.Start();
        _logger.LogInformation("DIMSE TLS proxy listening on port {Port}, forwarding to {Host}:{ForwardPort}",
            listenPort, orthancHost, orthancPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _ = HandleConnectionAsync(client, orthancHost, orthancPort, stoppingToken);
        }

        listener.Stop();
    }

    private async Task HandleConnectionAsync(TcpClient client, string orthancHost, int orthancPort, CancellationToken ct)
    {
        var remoteEp = client.Client.RemoteEndPoint;
        _logger.LogDebug("Incoming connection from {Remote}", remoteEp);

        SslStream ssl;
        X509Certificate2? clientCert = null;

        try
        {
            ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (_, cert, _, _) =>
                {
                    // Accept any cert here — we validate it against the ca-harness below.
                    // Returning false would abort the handshake before we can inspect the cert.
                    if (cert is not null)
                        clientCert = new X509Certificate2(cert);
                    return true;
                });

            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _serverCert.Certificate,
                ClientCertificateRequired = true,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("TLS handshake failed from {Remote}: {Message}", remoteEp, ex.Message);
            client.Dispose();
            return;
        }

        if (clientCert is null)
        {
            _logger.LogWarning("No client certificate presented from {Remote} — rejecting", remoteEp);
            ssl.Dispose();
            client.Dispose();
            return;
        }

        var certDer = Convert.ToBase64String(clientCert.RawData);
        var verification = await _caHarness.VerifyCertAsync(certDer, ct);

        if (verification is null)
        {
            _logger.LogWarning("Client cert from {Remote} (serial {Serial}) not recognized — rejecting",
                remoteEp, clientCert.SerialNumber);
            ssl.Dispose();
            client.Dispose();
            return;
        }

        _logger.LogInformation(
            "Authenticated: team={TeamName} backend={Backend} serial={Serial} — proxying to Orthanc",
            verification.TeamName, verification.Backend, verification.SerialNumber);

        // Record the scoring event (fire-and-forget — don't delay the proxy)
        _ = _caHarness.RecordCStoreEventAsync(
            verification.TeamToken, verification.Backend,
            verification.SerialNumber, verification.Thumbprint, ct);

        // Proxy raw DIMSE bytes bidirectionally to Orthanc plain port
        TcpClient orthanc;
        try
        {
            orthanc = new TcpClient();
            await orthanc.ConnectAsync(orthancHost, orthancPort, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Orthanc at {Host}:{Port}", orthancHost, orthancPort);
            ssl.Dispose();
            client.Dispose();
            return;
        }

        using (orthanc)
        using (ssl)
        using (client)
        {
            var orthancStream = orthanc.GetStream();
            try
            {
                await Task.WhenAny(
                    ssl.CopyToAsync(orthancStream, ct),
                    orthancStream.CopyToAsync(ssl, ct));
            }
            catch
            {
                // Normal when either side closes the connection
            }
        }

        _logger.LogDebug("Connection from {Remote} closed", remoteEp);
    }
}
