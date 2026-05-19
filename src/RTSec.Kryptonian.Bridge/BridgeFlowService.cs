using Microsoft.Extensions.Options;

namespace RTSec.Kryptonian.Bridge;

public class BridgeFlowService
{
    private readonly HttpClient _http;
    private readonly DicomWebMultipartParser _parser;
    private readonly DimseBridgeService _dimse;
    private readonly HarnessBridgeClient _harness;
    private readonly BridgeOptions _options;

    public BridgeFlowService(
        HttpClient http,
        DicomWebMultipartParser parser,
        DimseBridgeService dimse,
        HarnessBridgeClient harness,
        IOptions<BridgeOptions> options)
    {
        _http = http;
        _parser = parser;
        _dimse = dimse;
        _harness = harness;
        _options = options.Value;
    }

    public async Task<HttpResponseMessage> ForwardReceivedStoreToStowAsync(
        string backend,
        ReceivedDicomObject received,
        string certificateDerBase64,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(received);
        return await _harness.StowAsync(backend, received.DicomBytes, certificateDerBase64, ct);
    }

    public async Task<Flow3Result> RetrieveWadoStoreDimseAndClaimAsync(
        string studyInstanceUid,
        string seriesInstanceUid,
        string sopInstanceUid,
        CancellationToken ct = default)
    {
        var wadoUrl = $"{_options.DicomWebBaseUrl.TrimEnd('/')}/studies/{studyInstanceUid}/series/{seriesInstanceUid}/instances/{sopInstanceUid}";
        using var response = await _http.GetAsync(wadoUrl, ct);
        response.EnsureSuccessStatusCode();

        var parts = await _parser.ReadDicomPartsAsync(response.Content, ct);
        var storedSopInstanceUid = await _dimse.SendCStoreAsync(parts[0], ct);
        var claimResponse = await _harness.ClaimDicomWebToDimseAsync(storedSopInstanceUid, ct);
        var claimBody = await claimResponse.Content.ReadAsStringAsync(ct);

        return new Flow3Result(
            storedSopInstanceUid,
            (int)claimResponse.StatusCode,
            claimResponse.IsSuccessStatusCode,
            claimBody);
    }
}

public sealed record Flow3Result(
    string SopInstanceUid,
    int ClaimStatusCode,
    bool ClaimSucceeded,
    string ClaimResponseBody);
