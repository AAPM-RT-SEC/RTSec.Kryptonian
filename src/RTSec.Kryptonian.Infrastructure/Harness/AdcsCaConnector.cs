using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Cms;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;
using BcX509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace RTSec.Kryptonian.Infrastructure.Harness;

public sealed class AdcsCaConnector : ICaConnector
{
    private readonly ILogger<AdcsCaConnector> _logger;
    private readonly HttpClient _http;
    private readonly AdcsScepConnectorConfig _config;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CaBackendType Type => CaBackendType.Adcs;

    public AdcsCaConnector(ILogger<AdcsCaConnector> logger, HttpClient http, AdcsScepConnectorConfig config)
    {
        _logger = logger;
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<X509Certificate2[]> GetCaCertificatesAsync(CancellationToken ct = default)
    {
        var caCert = await GetScepCaCertAsync(ct);
        return new[] { new X509Certificate2(caCert.GetEncoded()) };
    }

    public async Task<CertificateIssuanceResult> IssueCertificateAsync(
        ParsedCsr csr,
        EstProfile profile,
        CancellationToken ct = default)
    {
        try
        {
            var caCert = await GetScepCaCertAsync(ct);
            using var scep = new ScepClient();
            var pkcsReq = scep.BuildPkcsReq(csr.RawData.ToArray(), caCert);

            using var body = new ByteArrayContent(pkcsReq);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/x-pki-message");

            using var request = new HttpRequestMessage(HttpMethod.Post, "scep/adcs?operation=PKIOperation");
            request.Content = body;
            request.Headers.Add("X-Device-Id", profile.Name);

            using var response = await _http.SendAsync(request, ct);
            var responseBytes = await response.Content.ReadAsByteArrayAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return CertificateIssuanceResult.Failed(
                    $"ADCS SCEP enrollment failed with {(int)response.StatusCode}: {System.Text.Encoding.UTF8.GetString(responseBytes)}");
            }

            var (certDer, error) = scep.ParseCertRep(responseBytes);
            if (certDer is null)
                return CertificateIssuanceResult.Failed(error ?? "ADCS SCEP response did not include a certificate");

            var cert = new X509Certificate2(certDer);
            return CertificateIssuanceResult.Successful(cert, new[] { cert });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ADCS SCEP enrollment failed");
            return CertificateIssuanceResult.Failed(ex.Message);
        }
    }

    public async Task<bool> RevokeCertificateAsync(string serial, RevocationReason reason, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "ADCS SCEP connector does not implement revocation. Revoke serial {Serial} in ADCS or expose a protocol-specific revocation integration.",
            serial);
        return await Task.FromResult(false);
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

    private async Task<BcX509Certificate> GetScepCaCertAsync(CancellationToken ct)
    {
        var resp = await _http.GetAsync("scep/adcs?operation=GetCACert", ct);
        resp.EnsureSuccessStatusCode();
        var p7 = await resp.Content.ReadAsByteArrayAsync(ct);
        var sd = new CmsSignedData(p7);
        var cert = sd.GetCertificates().EnumerateMatches(null).Cast<BcX509Certificate>().FirstOrDefault()
            ?? throw new InvalidOperationException("ADCS GetCACert response contained no certificate");
        return cert;
    }
}
