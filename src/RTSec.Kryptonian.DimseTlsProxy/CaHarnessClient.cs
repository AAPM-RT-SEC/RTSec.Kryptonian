using System.Net.Http.Json;

namespace RTSec.Kryptonian.DimseTlsProxy;

public sealed record VerifyResult(string TeamToken, string TeamName, string Backend, string SerialNumber, string Thumbprint);

// HTTP client for calling the ca-harness internal API.
// Both endpoints require the X-Internal-Key header.
public sealed class CaHarnessClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<CaHarnessClient> _logger;

    public CaHarnessClient(HttpClient http, IConfiguration config, ILogger<CaHarnessClient> logger)
    {
        _http = http;
        _apiKey = config["CA_HARNESS_API_KEY"] ?? throw new InvalidOperationException("CA_HARNESS_API_KEY is required");
        var baseUrl = config["CA_HARNESS_URL"] ?? throw new InvalidOperationException("CA_HARNESS_URL is required");
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _logger = logger;
    }

    public async Task<VerifyResult?> VerifyCertAsync(string certBase64Der, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/internal/dimse/verify-cert");
        req.Headers.Add("X-Internal-Key", _apiKey);
        req.Content = JsonContent.Create(new { certBase64Der });
        try
        {
            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<VerifyResult>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VerifyCert call to ca-harness failed");
            return null;
        }
    }

    public async Task RecordCStoreEventAsync(string teamToken, string backend, string serialNumber, string thumbprint, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/internal/dimse/cstore-event");
        req.Headers.Add("X-Internal-Key", _apiKey);
        req.Content = JsonContent.Create(new { teamToken, backend, serialNumber, thumbprint });
        try
        {
            await _http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RecordCStoreEvent call to ca-harness failed");
        }
    }
}
