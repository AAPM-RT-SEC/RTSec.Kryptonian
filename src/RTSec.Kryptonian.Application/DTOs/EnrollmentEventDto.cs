using System.Text.Json.Serialization;

namespace RTSec.Kryptonian.Application.DTOs;

/// <summary>
/// DTO for enrollment event response. Matches OpenAPI.Schema.yaml EnrollmentEvent.
/// </summary>
public class EnrollmentEventDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("subjectDn")]
    public string? SubjectDn { get; set; }

    [JsonPropertyName("requestorIpAddress")]
    public string? RequestorIpAddress { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("issuedCertificateId")]
    public string? IssuedCertificateId { get; set; }
}
