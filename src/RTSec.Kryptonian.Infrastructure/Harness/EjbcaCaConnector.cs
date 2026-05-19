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

public sealed class EjbcaCaConnector : ICaConnector
{
    private readonly ILogger<EjbcaCaConnector> _logger;
    private readonly HttpClient _http;
    private readonly EjbcaRestConnectorConfig _config;
    private X509Certificate2[]? _cachedChain;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CaBackendType Type => CaBackendType.Ejbca;

    public EjbcaCaConnector(ILogger<EjbcaCaConnector> logger, HttpClient http, EjbcaRestConnectorConfig config)
    {
        _logger = logger;
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
    }

    public Task<X509Certificate2[]> GetCaCertificatesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_cachedChain ?? Array.Empty<X509Certificate2>());
    }

    public async Task<CertificateIssuanceResult> IssueCertificateAsync(
        ParsedCsr csr,
        EstProfile profile,
        CancellationToken ct = default)
    {
        var request = new EjbcaRestEnrollRequest
        {
            CertificateRequest = ToPem(csr.RawData.ToArray(), "CERTIFICATE REQUEST"),
            CertificateProfileName = _config.CertificateProfile,
            EndEntityProfileName = _config.EndEntityProfile,
            Username = profile.Name,
            IncludeChain = true
        };

        EjbcaRestEnrollResponse? response;
        try
        {
            var httpResponse = await _http.PostAsJsonAsync(
                "ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll",
                request,
                JsonOpts,
                ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = await httpResponse.Content.ReadAsStringAsync(ct);
                return CertificateIssuanceResult.Failed(
                    $"EJBCA REST enrollment failed with {(int)httpResponse.StatusCode}: {error}");
            }

            response = await httpResponse.Content.ReadFromJsonAsync<EjbcaRestEnrollResponse>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EJBCA REST enrollment request failed");
            return CertificateIssuanceResult.Failed(ex.Message);
        }

        return ParseResponse(response);
    }

    public async Task<bool> RevokeCertificateAsync(string serial, RevocationReason reason, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_config.IssuerDn))
            {
                _logger.LogWarning("EJBCA REST revoke requires IssuerDn config. Serial: {Serial}", serial);
                return false;
            }

            var requestUri =
                $"ejbca/ejbca-rest-api/v1/certificate/{Uri.EscapeDataString(_config.IssuerDn)}/{Uri.EscapeDataString(serial)}/revoke";
            var resp = await _http.PutAsJsonAsync(requestUri, new { reason = reason.ToString() }, JsonOpts, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EJBCA REST revoke failed for serial {Serial}", serial);
            return false;
        }
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

    private CertificateIssuanceResult ParseResponse(EjbcaRestEnrollResponse? response)
    {
        if (response is null)
            return CertificateIssuanceResult.Failed("Empty response from EJBCA REST endpoint");

        if (!string.IsNullOrWhiteSpace(response.Certificate))
        {
            try
            {
                var cert = new X509Certificate2(Convert.FromBase64String(response.Certificate));
                var chain = ParseChain(response.CertificateChain);
                _cachedChain = chain.Length > 0 ? chain : new[] { cert };
                return CertificateIssuanceResult.Successful(cert, _cachedChain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse certificate from EJBCA REST response");
                return CertificateIssuanceResult.Failed($"Certificate parse failed: {ex.Message}");
            }
        }

        return CertificateIssuanceResult.Failed("EJBCA REST response did not include a certificate");
    }

    private static string ToPem(byte[] der, string label)
    {
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        return $"-----BEGIN {label}-----\n{base64}\n-----END {label}-----";
    }

    private static X509Certificate2[] ParseChain(IReadOnlyList<string>? certificateChain)
    {
        if (certificateChain is null || certificateChain.Count == 0)
            return Array.Empty<X509Certificate2>();

        return certificateChain
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Contains("BEGIN CERTIFICATE", StringComparison.Ordinal)
                ? X509Certificate2.CreateFromPem(c)
                : new X509Certificate2(Convert.FromBase64String(c)))
            .ToArray();
    }
}
