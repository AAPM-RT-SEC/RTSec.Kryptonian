using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using System.Text;
using Microsoft.Extensions.Options;

namespace RTSec.Kryptonian.Bridge;

public class DimseBridgeService
{
    private readonly IDicomClientFactory _clientFactory;
    private readonly IDicomServerFactory _serverFactory;
    private readonly BridgeOptions _options;
    private readonly ILogger<DimseBridgeService> _logger;

    public DimseBridgeService(
        IDicomClientFactory clientFactory,
        IDicomServerFactory serverFactory,
        IOptions<BridgeOptions> options,
        ILogger<DimseBridgeService> logger)
    {
        _clientFactory = clientFactory;
        _serverFactory = serverFactory;
        _options = options.Value;
        _logger = logger;
    }

    public IDicomServer StartStoreScp(StoreScpState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _serverFactory.Create<BridgeStoreScp>(
            _options.BridgeListenPort,
            tlsAcceptor: null,
            fallbackEncoding: Encoding.UTF8,
            logger: null,
            userState: state,
            configure: null);
    }

    public async Task SendCMoveAsync(
        string studyInstanceUid,
        string? seriesInstanceUid = null,
        string? sopInstanceUid = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            throw new ArgumentException("Study Instance UID is required.", nameof(studyInstanceUid));
        }

        var request = string.IsNullOrWhiteSpace(seriesInstanceUid)
            ? new DicomCMoveRequest(_options.BridgeAeTitle, studyInstanceUid)
            : string.IsNullOrWhiteSpace(sopInstanceUid)
                ? new DicomCMoveRequest(_options.BridgeAeTitle, studyInstanceUid, seriesInstanceUid)
                : new DicomCMoveRequest(_options.BridgeAeTitle, studyInstanceUid, seriesInstanceUid, sopInstanceUid);

        request.OnResponseReceived = (_, response) =>
        {
            _logger.LogInformation(
                "C-MOVE response: status={Status}, remaining={Remaining}, completed={Completed}, failed={Failed}",
                response.Status,
                response.Remaining,
                response.Completed,
                response.Failures);
        };

        var client = _clientFactory.Create(
            _options.DimseHost,
            _options.DimsePort,
            useTls: false,
            callingAe: _options.BridgeAeTitle,
            calledAe: _options.CalledAeTitle);

        await client.AddRequestAsync(request);
        await client.SendAsync(ct, DicomClientCancellationMode.ImmediatelyReleaseAssociation);
    }

    public async Task<string> SendCStoreAsync(byte[] dicomBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dicomBytes);

        await using var stream = new MemoryStream(dicomBytes);
        var file = await DicomFile.OpenAsync(stream, FileReadOption.ReadAll);
        var sopInstanceUid = file.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty);
        if (string.IsNullOrWhiteSpace(sopInstanceUid))
        {
            throw new InvalidOperationException("DICOM object does not contain SOPInstanceUID.");
        }

        var request = new DicomCStoreRequest(file);
        request.OnResponseReceived = (_, response) =>
        {
            _logger.LogInformation("C-STORE response for {SopInstanceUid}: {Status}", sopInstanceUid, response.Status);
        };

        var client = _clientFactory.Create(
            _options.DimseHost,
            _options.DimsePort,
            useTls: false,
            callingAe: _options.BridgeAeTitle,
            calledAe: _options.CalledAeTitle);

        await client.AddRequestAsync(request);
        await client.SendAsync(ct, DicomClientCancellationMode.ImmediatelyReleaseAssociation);

        return sopInstanceUid;
    }
}
