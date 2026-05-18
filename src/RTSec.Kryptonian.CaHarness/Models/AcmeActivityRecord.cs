namespace RTSec.Kryptonian.CaHarness.Models;

public sealed record AcmeActivityRecord(
    string Id,
    DateTime TimestampUtc,
    string Operation,
    string Method,
    string Path,
    int StatusCode);
