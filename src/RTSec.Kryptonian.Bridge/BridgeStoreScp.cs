using System.Text;
using FellowOakDicom;
using FellowOakDicom.Network;

namespace RTSec.Kryptonian.Bridge;

public class BridgeStoreScp : DicomService, IDicomServiceProvider, IDicomCStoreProvider
{
    public BridgeStoreScp(
        INetworkStream stream,
        Encoding fallbackEncoding,
        ILogger log,
        DicomServiceDependencies dependencies)
        : base(stream, fallbackEncoding, log, dependencies)
    {
    }

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        foreach (var presentationContext in association.PresentationContexts)
        {
            presentationContext.AcceptTransferSyntaxes(
                DicomTransferSyntax.ExplicitVRLittleEndian,
                DicomTransferSyntax.ImplicitVRLittleEndian,
                DicomTransferSyntax.ExplicitVRBigEndian);
        }

        return SendAssociationAcceptAsync(association);
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
        => SendAssociationReleaseResponseAsync();

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
    {
        Logger.LogWarning("Association aborted by {Source}: {Reason}", source, reason);
    }

    public void OnConnectionClosed(Exception exception)
    {
        if (exception != null)
        {
            Logger.LogDebug(exception, "DICOM association closed with exception");
        }
    }

    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        if (request.File == null)
        {
            return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
        }

        await using var output = new MemoryStream();
        await request.File.SaveAsync(output);

        if (UserState is StoreScpState state)
        {
            var sopInstanceUid = request.SOPInstanceUID?.UID
                ?? request.File.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty);

            await state.RecordAsync(new ReceivedDicomObject(
                sopInstanceUid,
                output.ToArray(),
                DateTime.UtcNow));
        }

        return new DicomCStoreResponse(request, DicomStatus.Success);
    }

    public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
    {
        Logger.LogError(e, "C-STORE request failed while reading {TempFileName}", tempFileName);
        return Task.CompletedTask;
    }
}
