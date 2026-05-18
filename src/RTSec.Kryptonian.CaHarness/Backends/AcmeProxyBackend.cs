using System.Collections.Concurrent;
using System.Text;
using RTSec.Kryptonian.CaHarness.Models;

namespace RTSec.Kryptonian.CaHarness.Backends;

public sealed class AcmeProxyBackend : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _upstreamBaseUrl;
    private readonly string? _harnessBaseUrl;
    private readonly string? _teamPath;
    private readonly ConcurrentQueue<AcmeActivityRecord> _activity = new();
    // Certs explicitly claimed by the team via POST .../acme/claim
    private readonly ConcurrentQueue<AcmeActivityRecord> _claimedCerts = new();

    public AcmeProxyBackend(string upstreamBaseUrl, string? harnessBaseUrl = null, string? teamPath = null)
    {
        _upstreamBaseUrl = upstreamBaseUrl.TrimEnd('/');
        _harnessBaseUrl = harnessBaseUrl?.TrimEnd('/');
        _teamPath = teamPath;

        // Disable cert validation — step-ca uses a self-signed cert
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task ProxyAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        var query = context.Request.QueryString.Value ?? "";
        var upstreamUrl = _upstreamBaseUrl + path + query;

        using var upstreamRequest = new HttpRequestMessage(
            new HttpMethod(context.Request.Method), upstreamUrl);

        // Inject harness Host header so step-ca validates JWS url against the public hostname.
        // ACME JWS protected.url must match what step-ca sees as the request URL
        // (scheme + Host header + path). Without this, step-ca rejects requests as "malformed".
        if (_harnessBaseUrl is not null
            && Uri.TryCreate(_harnessBaseUrl, UriKind.Absolute, out var harnessUri))
        {
            upstreamRequest.Headers.Host = harnessUri.Host;
        }

        // Forward body
        if (context.Request.ContentLength is > 0
            || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            upstreamRequest.Content = new StreamContent(context.Request.Body);
            if (!string.IsNullOrEmpty(context.Request.ContentType))
                upstreamRequest.Content.Headers.TryAddWithoutValidation(
                    "Content-Type", context.Request.ContentType);
        }

        // Forward headers (skip hop-by-hop and Host — Host is set explicitly above)
        foreach (var (key, value) in context.Request.Headers)
        {
            if (IsHopByHop(key) || key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                continue;
            upstreamRequest.Headers.TryAddWithoutValidation(key, value.ToArray());
        }

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await _http.SendAsync(
                upstreamRequest, HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 502;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "ACME upstream unavailable",
                upstream = _upstreamBaseUrl,
                detail = ex.Message
            });
            return;
        }

        using (upstreamResponse)
        {
            var responseBytes = await upstreamResponse.Content
                .ReadAsByteArrayAsync(context.RequestAborted);

            // Rewrite directory: replace step-ca internal URLs with harness public base.
            // URLs are kept as /acme/acme/{resource} (no team token) so the ACME client
            // signs JWS with URLs that match what step-ca sees (Host header = harness host).
            if (path.EndsWith("/directory", StringComparison.OrdinalIgnoreCase)
                && responseBytes.Length > 0)
            {
                var harnessBase = _harnessBaseUrl
                    ?? $"{context.Request.Scheme}://{context.Request.Host}";
                // Replace upstream base URL — keep the /acme/acme path as-is.
                // Result: https://step-ca:9000/acme/acme/new-account
                //      → https://ca-harness.../acme/acme/new-account
                var rewritten = Encoding.UTF8.GetString(responseBytes)
                    .Replace(_upstreamBaseUrl, harnessBase, StringComparison.Ordinal);
                responseBytes = Encoding.UTF8.GetBytes(rewritten);
            }

            // Log significant operations (skip HEAD nonce requests — pure protocol noise)
            var operation = ClassifyOperation(context.Request.Method, path);
            if (operation is not null)
            {
                _activity.Enqueue(new AcmeActivityRecord(
                    Id: Guid.NewGuid().ToString("N")[..8],
                    TimestampUtc: DateTime.UtcNow,
                    Operation: operation,
                    Method: context.Request.Method,
                    Path: path,
                    StatusCode: (int)upstreamResponse.StatusCode));
            }

            // Write response headers
            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            foreach (var (key, value) in upstreamResponse.Headers
                .Concat(upstreamResponse.Content.Headers))
            {
                if (IsHopByHop(key) || key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    continue;
                context.Response.Headers[key] = value.ToArray();
            }
            context.Response.ContentLength = responseBytes.Length;

            await context.Response.Body.WriteAsync(responseBytes, context.RequestAborted);
        }
    }

    // Record an ACME cert that the team explicitly submitted via the claim endpoint.
    public void RecordClaim(string serial, string thumbprint) =>
        _claimedCerts.Enqueue(new AcmeActivityRecord(
            Id: Guid.NewGuid().ToString("N")[..8],
            TimestampUtc: DateTime.UtcNow,
            Operation: "certificate-claimed",
            Method: "POST",
            Path: "/claim",
            StatusCode: 200));

    public IReadOnlyList<AcmeActivityRecord> GetActivity() =>
        [.. _activity, .. _claimedCerts];

    // Returns claimed certs — used by scoreboard to mark ACME backend as complete.
    public IReadOnlyList<AcmeActivityRecord> GetIssued() => _claimedCerts.ToList();

    public void Reset()
    {
        _activity.Clear();
        _claimedCerts.Clear();
    }

    public void Dispose() => _http.Dispose();

    private static string? ClassifyOperation(string method, string path)
    {
        // Skip HEAD entirely — it's just nonce fetching, too noisy to log
        if (method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) return null;

        if (path.EndsWith("/directory", StringComparison.OrdinalIgnoreCase))
            return "directory-fetch";
        if (path.Contains("/new-account", StringComparison.OrdinalIgnoreCase))
            return "new-account";
        if (path.Contains("/new-order", StringComparison.OrdinalIgnoreCase))
            return "new-order";
        if (path.Contains("/challenge/", StringComparison.OrdinalIgnoreCase))
            return "challenge-response";
        if (path.EndsWith("/finalize", StringComparison.OrdinalIgnoreCase))
            return "order-finalized";
        if (path.Contains("/cert/", StringComparison.OrdinalIgnoreCase))
            return "certificate-downloaded";
        if (path.Contains("/authz/", StringComparison.OrdinalIgnoreCase))
            return "authorization";
        if (path.Contains("/order/", StringComparison.OrdinalIgnoreCase))
            return "order-status";

        return null;
    }

    private static bool IsHopByHop(string header) =>
        header.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
        header.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
        header.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
        header.Equals("TE", StringComparison.OrdinalIgnoreCase) ||
        header.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) ||
        header.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
        header.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase) ||
        header.Equals("Trailer", StringComparison.OrdinalIgnoreCase);
}
