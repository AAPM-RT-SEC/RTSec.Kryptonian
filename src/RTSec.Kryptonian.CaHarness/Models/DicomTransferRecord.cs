namespace RTSec.Kryptonian.CaHarness.Models;

public sealed record DicomTransferRecord(
    string Token,
    string Backend,
    string DeviceId,
    string SerialNumber,
    string Thumbprint,
    bool UsedGateway,
    DateTime ReceivedUtc,
    string SopInstanceUid);
