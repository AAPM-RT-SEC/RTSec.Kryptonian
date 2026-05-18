namespace RTSec.Kryptonian.CaHarness.Models;

public class IssueResponse
{
    public string Status { get; init; } = string.Empty;
    public string Backend { get; init; } = string.Empty;

    // issued
    public string? Issuer { get; init; }
    public string? SerialNumber { get; init; }
    public string? Thumbprint { get; init; }
    public DateTime? NotBeforeUtc { get; init; }
    public DateTime? NotAfterUtc { get; init; }
    public string? CertificatePem { get; init; }
    public string? CertificateDerBase64 { get; init; }
    public IReadOnlyList<string>? CaChainPem { get; init; }

    // rejected
    public string? ReasonCode { get; init; }

    // pending
    public string? RequestId { get; init; }

    // all
    public string? Message { get; init; }
}
