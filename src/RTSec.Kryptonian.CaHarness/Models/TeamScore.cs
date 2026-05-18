namespace RTSec.Kryptonian.CaHarness.Models;

public sealed record BackendScore(
    string Backend,
    string Status,
    int CertsIssued,
    int Resets,
    bool UsedGateway,
    DateTime? FirstDicomUtc,
    bool DimseStoreTlsComplete);

public sealed record ManualScore(
    bool DeviceRegistration,
    bool PendingStatus,
    bool DeviceRemoval)
{
    public int Total => (DeviceRegistration ? 1 : 0) + (PendingStatus ? 1 : 0) + (DeviceRemoval ? 1 : 0);
}

public sealed record TeamScore(
    string TeamName,
    IReadOnlyList<BackendScore> Backends,
    int BackendsComplete,
    DateTime? FirstDicomUtc,
    int OverallRank,
    bool CmoveComplete,
    bool DicomWebToDimseComplete,
    ManualScore Manual);
