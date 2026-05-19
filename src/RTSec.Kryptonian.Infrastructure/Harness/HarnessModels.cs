using System.Text.Json.Serialization;

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

internal sealed class EjbcaRestEnrollRequest
{
    [JsonPropertyName("certificate_request")]
    public string CertificateRequest { get; init; } = string.Empty;

    [JsonPropertyName("certificate_profile_name")]
    public string? CertificateProfileName { get; init; }

    [JsonPropertyName("end_entity_profile_name")]
    public string? EndEntityProfileName { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("include_chain")]
    public bool? IncludeChain { get; init; }
}

internal sealed class EjbcaRestEnrollResponse
{
    [JsonPropertyName("certificate")]
    public string? Certificate { get; init; }

    [JsonPropertyName("certificate_chain")]
    public IReadOnlyList<string>? CertificateChain { get; init; }

    [JsonPropertyName("serial_number")]
    public string? SerialNumber { get; init; }
}
