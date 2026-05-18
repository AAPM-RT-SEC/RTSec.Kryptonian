namespace RTSec.Kryptonian.CaHarness.Models;

public class IssueRequest
{
    public string CsrBase64Der { get; init; } = string.Empty;
    public string? ProfileName { get; init; }
    public string? TemplateName { get; init; }
    public int ValidityDays { get; init; } = 7;
    public string? SubjectOverride { get; init; }
    public string? DeviceId { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public class RevokeRequest
{
    public string SerialNumber { get; init; } = string.Empty;
    public string? Reason { get; init; }
}
