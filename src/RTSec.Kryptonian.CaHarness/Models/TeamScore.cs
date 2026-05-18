namespace RTSec.Kryptonian.CaHarness.Models;

public sealed record BackendScore(
    string Backend,
    string Status,
    int CertsIssued,
    int Resets,
    bool UsedGateway,
    DateTime? FirstDicomUtc,
    bool DimseStoreTlsComplete);

public sealed record TeamScore(
    string TeamName,
    IReadOnlyList<BackendScore> Backends,
    int BackendsComplete,
    DateTime? FirstDicomUtc,
    int OverallRank,
    bool CmoveComplete,
    bool DicomWebToDimseComplete);
