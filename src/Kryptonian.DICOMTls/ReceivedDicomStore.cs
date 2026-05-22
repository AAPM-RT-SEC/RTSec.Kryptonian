using System.Collections.Concurrent;
using FellowOakDicom;

namespace Kryptonian.DICOMTls;

internal sealed record ReceivedDicomObject(string SopInstanceUid, string Path, DateTimeOffset ReceivedAt);

internal sealed class ReceivedDicomStore
{
    private readonly ConcurrentBag<ReceivedDicomObject> _received = new();

    public ReceivedDicomStore(MedicalDeviceNode node, string outputPath)
    {
        Node = node;
        OutputPath = outputPath;
        Directory.CreateDirectory(OutputPath);
    }

    public MedicalDeviceNode Node { get; }

    public string OutputPath { get; }

    public IReadOnlyCollection<ReceivedDicomObject> Received => _received.ToArray();

    public async Task RecordAsync(DicomFile file)
    {
        var sopInstanceUid = file.Dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID);
        var path = Path.Combine(OutputPath, $"{sopInstanceUid}.dcm");
        await file.SaveAsync(path);
        _received.Add(new ReceivedDicomObject(sopInstanceUid, path, DateTimeOffset.UtcNow));
    }

    public async Task<IReadOnlyCollection<ReceivedDicomObject>> WaitForAsync(int count, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = Received;
            if (snapshot.Count >= count)
            {
                return snapshot;
            }

            await Task.Delay(100);
        }

        return Received;
    }
}
