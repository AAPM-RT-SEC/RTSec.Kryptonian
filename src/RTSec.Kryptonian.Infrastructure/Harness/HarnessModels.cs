namespace RTSec.Kryptonian.Infrastructure.Harness;

internal sealed class HarnessIssueRequest
{
    public string CsrBase64Der { get; init; } = string.Empty;
    public string? ProfileName { get; init; }
    public string? TemplateName { get; init; }
    public int ValidityDays { get; init; } = 7;
    public string? DeviceId { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

internal sealed class HarnessIssueResponse
{
    public string Status { get; init; } = string.Empty;
    public string? Issuer { get; init; }
    public string? SerialNumber { get; init; }
    public string? Thumbprint { get; init; }
    public DateTime? NotBeforeUtc { get; init; }
    public DateTime? NotAfterUtc { get; init; }
    public string? CertificatePem { get; init; }
    public string? CertificateDerBase64 { get; init; }
    public IReadOnlyList<string>? CaChainPem { get; init; }
    public string? Message { get; init; }
    public string? ReasonCode { get; init; }
    public string? RequestId { get; init; }
}
