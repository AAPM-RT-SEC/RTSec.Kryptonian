using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.ValueObjects;

public sealed record CrlEntry(string SerialNumber, DateTime RevokedAt, RevocationReason Reason);

public sealed record CrlGenerationRequest(
    string ExpectedIssuerFingerprint,
    long CrlNumber,
    DateTime ThisUpdate,
    DateTime NextUpdate,
    IReadOnlyCollection<CrlEntry> Entries);

public sealed record CrlGenerationResult(bool Supported, string? IssuerFingerprint, byte[]? Der, string? ErrorMessage)
{
    public static CrlGenerationResult Unsupported(string? error = null) => new(false, null, null, error);
    public static CrlGenerationResult Successful(string issuerFingerprint, byte[] der) => new(true, issuerFingerprint, der, null);
}
