using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace RTSec.Kryptonian.Bridge;

public class HarnessBridgeClient
{
    private readonly HttpClient _http;
    private readonly BridgeOptions _options;

    public HarnessBridgeClient(HttpClient http, IOptions<BridgeOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<HttpResponseMessage> StowAsync(
        string backend,
        byte[] dicomBytes,
        string certificateDerBase64,
        CancellationToken ct = default)
    {
        RequireTeamToken();

        using var content = new ByteArrayContent(dicomBytes);
        content.Headers.ContentType = new("application/dicom");
        content.Headers.Add("X-Device-Certificate", certificateDerBase64);
        content.Headers.Add("X-Transfer-Mode", "cmove");

        var url = $"{TrimBaseUrl()}/teams/{_options.TeamToken}/dicom/backends/{backend}/stow";
        return await _http.PostAsync(url, content, ct);
    }

    public async Task<HttpResponseMessage> ClaimDicomWebToDimseAsync(string sopInstanceUid, CancellationToken ct = default)
    {
        RequireTeamToken();

        var url = $"{TrimBaseUrl()}/teams/{_options.TeamToken}/scoring/dicomweb-to-dimse";
        return await _http.PostAsJsonAsync(url, new { sopInstanceUid }, ct);
    }

    public async Task<HttpResponseMessage> ClaimAcmeAsync(string certificatePem, CancellationToken ct = default)
    {
        RequireTeamToken();

        var url = $"{TrimBaseUrl()}/teams/{_options.TeamToken}/api/backends/acme/claim";
        return await _http.PostAsJsonAsync(url, new { certificate = certificatePem }, ct);
    }

    private string TrimBaseUrl() => _options.HarnessBaseUrl.TrimEnd('/');

    private void RequireTeamToken()
    {
        if (string.IsNullOrWhiteSpace(_options.TeamToken))
        {
            throw new InvalidOperationException("Bridge:TeamToken is required for harness calls.");
        }
    }
}
