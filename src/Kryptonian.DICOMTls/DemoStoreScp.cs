using System.Text;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Extensions.Logging;

namespace Kryptonian.DICOMTls;

internal sealed class DemoStoreScp : DicomService, IDicomServiceProvider, IDicomCStoreProvider
{
    public DemoStoreScp(
        INetworkStream stream,
        Encoding fallbackEncoding,
        ILogger log,
        DicomServiceDependencies dependencies)
        : base(stream, fallbackEncoding, log, dependencies)
    {
    }

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        if (UserState is ReceivedDicomStore store
            && !string.Equals(association.CalledAE, store.Node.AeTitle, StringComparison.OrdinalIgnoreCase))
        {
            return SendAssociationRejectAsync(
                DicomRejectResult.Permanent,
                DicomRejectSource.ServiceUser,
                DicomRejectReason.CalledAENotRecognized);
        }

        foreach (var presentationContext in association.PresentationContexts)
        {
            presentationContext.AcceptTransferSyntaxes(
                DicomTransferSyntax.ExplicitVRLittleEndian,
                DicomTransferSyntax.ImplicitVRLittleEndian,
                DicomTransferSyntax.ExplicitVRBigEndian);
        }

        Console.WriteLine($"Association accepted: {association.CallingAE} -> {association.CalledAE}");
        return SendAssociationAcceptAsync(association);
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
        => SendAssociationReleaseResponseAsync();

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        => Console.WriteLine($"Association aborted by {source}: {reason}");

    public void OnConnectionClosed(Exception exception)
    {
        if (exception != null)
        {
            Console.WriteLine($"Association closed with exception: {exception.Message}");
        }
    }

    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        if (request.File == null || UserState is not ReceivedDicomStore store)
        {
            return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
        }

        await store.RecordAsync(request.File);
        return new DicomCStoreResponse(request, DicomStatus.Success);
    }

    public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
    {
        Console.WriteLine($"C-STORE request failed while reading {tempFileName}: {e.Message}");
        return Task.CompletedTask;
    }
}
