using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Infrastructure.Harness;

public sealed class AdcsCaConnector : ICaConnector
{
    private readonly ILogger<AdcsCaConnector> _logger;
    private readonly HttpClient _http;
    private readonly AdcsHarnessConnectorConfig _config;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CaBackendType Type => CaBackendType.Adcs;

    public AdcsCaConnector(ILogger<AdcsCaConnector> logger, HttpClient http, AdcsHarnessConnectorConfig config)
    {
        _logger = logger;
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri(config.HarnessBaseUrl.TrimEnd('/') + "/");
    }

    public async Task<X509Certificate2[]> GetCaCertificatesAsync(CancellationToken ct = default)
    {
        var pems = await _http.GetFromJsonAsync<string[]>("api/backends/adcs/cacerts", JsonOpts, ct)
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
        var request = new HarnessIssueRequest
        {
            CsrBase64Der = Convert.ToBase64String(csr.RawData.ToArray()),
            TemplateName = _config.TemplateName,
            ValidityDays = _config.ValidityDays,
            DeviceId = profile.Name,
            Metadata = new Dictionary<string, string> { ["requestedBy"] = "gateway" }
        };

        HarnessIssueResponse? response;
        try
        {
            var httpResponse = await _http.PostAsJsonAsync("api/backends/adcs/issue", request, JsonOpts, ct);
            response = await httpResponse.Content.ReadFromJsonAsync<HarnessIssueResponse>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ADCS harness issue request failed");
            return CertificateIssuanceResult.Failed(ex.Message);
        }

        return ParseResponse(response);
    }

    public async Task<bool> RevokeCertificateAsync(string serial, RevocationReason reason, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/backends/adcs/revoke",
                new { serialNumber = serial, reason = reason.ToString() }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ADCS revoke failed for serial {Serial}", serial);
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private CertificateIssuanceResult ParseResponse(HarnessIssueResponse? response)
    {
        if (response is null)
            return CertificateIssuanceResult.Failed("Empty response from ADCS harness");

        if (response.Status == "issued" && !string.IsNullOrEmpty(response.CertificatePem))
        {
            try
            {
                var cert = X509Certificate2.CreateFromPem(response.CertificatePem);
                var chain = (response.CaChainPem ?? Array.Empty<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => X509Certificate2.CreateFromPem(p))
                    .ToArray();
                return CertificateIssuanceResult.Successful(cert, chain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse certificate from ADCS harness response");
                return CertificateIssuanceResult.Failed($"Certificate parse failed: {ex.Message}");
            }
        }

        if (response.Status == "pending")
            return CertificateIssuanceResult.Pending(30);

        return CertificateIssuanceResult.Failed(
            response.Message ?? $"ADCS harness rejected: {response.ReasonCode}");
    }
}
