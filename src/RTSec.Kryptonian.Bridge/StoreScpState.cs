using System.Threading.Channels;

namespace RTSec.Kryptonian.Bridge;

public sealed class StoreScpState
{
    private readonly Channel<ReceivedDicomObject> _received = Channel.CreateUnbounded<ReceivedDicomObject>();

    public ChannelReader<ReceivedDicomObject> Received => _received.Reader;

    public ValueTask RecordAsync(ReceivedDicomObject received, CancellationToken ct = default)
        => _received.Writer.WriteAsync(received, ct);
}

public sealed record ReceivedDicomObject(
    string SopInstanceUid,
    byte[] DicomBytes,
    DateTime ReceivedAtUtc);
