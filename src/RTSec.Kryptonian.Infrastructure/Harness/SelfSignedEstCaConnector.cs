using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Infrastructure.Harness;

public sealed class SelfSignedEstCaConnector : ICaConnector
{
    private readonly ILogger<SelfSignedEstCaConnector> _logger;
    private readonly HttpClient _http;
    private readonly SelfSignedEstConnectorConfig _config;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CaBackendType Type => CaBackendType.SelfSigned;

    public SelfSignedEstCaConnector(
        ILogger<SelfSignedEstCaConnector> logger,
        HttpClient http,
        SelfSignedEstConnectorConfig config)
    {
        _logger = logger;
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri(config.HarnessBaseUrl.TrimEnd('/') + "/");
    }

    public async Task<X509Certificate2[]> GetCaCertificatesAsync(CancellationToken ct = default)
    {
        var pems = await _http.GetFromJsonAsync<string[]>("api/backends/selfsigned/cacerts", JsonOpts, ct)
            ?? Array.Empty<string>();

        return pems
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => X509Certificate2.CreateFromPem(p))
            .ToArray();
    }

    public async Task<CertificateIssuanceResult> IssueCertificateAsync(
        ParsedCsr csr,
        EstProfile profile,
        CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "est/selfsigned/simpleenroll");
            request.Headers.Add("X-Device-Id", profile.Name);
            request.Content = new StringContent(Convert.ToBase64String(csr.RawData.ToArray()), Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return CertificateIssuanceResult.Failed(
                    $"SelfSigned EST enrollment failed with {(int)response.StatusCode}: {body}");
            }

            var result = JsonSerializer.Deserialize<HarnessIssueResponse>(body, JsonOpts);
            if (result is null)
                return CertificateIssuanceResult.Failed("Empty response from SelfSigned EST harness");

            if (!string.Equals(result.Status, "issued", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(result.CertificateDerBase64))
            {
                return CertificateIssuanceResult.Failed(
                    result.Message ?? "SelfSigned EST response did not include an issued certificate");
            }

            var cert = new X509Certificate2(Convert.FromBase64String(result.CertificateDerBase64));
            var chain = new List<X509Certificate2> { cert };
            if (result.CaChainPem is not null)
            {
                chain.AddRange(result.CaChainPem
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => X509Certificate2.CreateFromPem(p)));
            }

            return CertificateIssuanceResult.Successful(cert, chain.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SelfSigned EST enrollment failed");
            return CertificateIssuanceResult.Failed(ex.Message);
        }
    }

    public Task<bool> RevokeCertificateAsync(string serial, RevocationReason reason, CancellationToken ct = default)
    {
        _logger.LogWarning("SelfSigned EST revoke is not supported by the harness connector. Serial: {Serial}", serial);
        return Task.FromResult(false);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class SelfSignedEstConnectorConfig
{
    public string HarnessBaseUrl { get; init; } = string.Empty;
}
