using System.Net;
using System.Text;

namespace RTSec.Kryptonian.DimseTlsProxy;

// Serves the proxy's TLS server certificate on port 8044 as plain PEM.
// Teams GET http://{vm-ip}:8044/server-cert to download the cert and add it to their trust store.
public sealed class CertHttpWorker : BackgroundService
{
    private readonly ServerCertificateProvider _serverCert;
    private readonly ILogger<CertHttpWorker> _logger;

    public CertHttpWorker(ServerCertificateProvider serverCert, ILogger<CertHttpWorker> logger)
    {
        _serverCert = serverCert;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://*:8044/");
        listener.Start();
        _logger.LogInformation("Proxy cert HTTP server listening on port 8044 — GET /server-cert");

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            if (path == "/server-cert" || path == "/")
            {
                var pem = Encoding.UTF8.GetBytes(_serverCert.CertificatePem);
                ctx.Response.ContentType = "application/x-pem-file";
                ctx.Response.ContentLength64 = pem.Length;
                ctx.Response.Headers.Add("Content-Disposition", "attachment; filename=\"dimse-proxy-server.pem\"");
                await ctx.Response.OutputStream.WriteAsync(pem, stoppingToken);
            }
            else
            {
                ctx.Response.StatusCode = 404;
            }
            ctx.Response.Close();
        }

        listener.Stop();
    }
}
