using System.Text.Json.Serialization;

namespace RTSec.Kryptonian.Application.DTOs;

public class GatewaySettingsDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("defaultCertificateLifetimeHours")]
    public int DefaultCertificateLifetimeHours { get; set; }

    [JsonPropertyName("minCertificateLifetimeHours")]
    public int MinCertificateLifetimeHours { get; set; }

    [JsonPropertyName("maxCertificateLifetimeHours")]
    public int MaxCertificateLifetimeHours { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class GatewaySettingsUpdateDto
{
    [JsonPropertyName("defaultCertificateLifetimeHours")]
    public int DefaultCertificateLifetimeHours { get; set; }
}
