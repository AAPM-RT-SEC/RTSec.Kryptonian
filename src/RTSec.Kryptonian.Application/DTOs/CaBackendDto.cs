using System.Text.Json.Serialization;

namespace RTSec.Kryptonian.Application.DTOs;

/// <summary>
/// DTO for CA backend response. Matches OpenAPI.Schema.yaml CaBackend.
/// </summary>
public class CaBackendDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("config")]
    public Dictionary<string, object>? Config { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating a CA backend. Matches OpenAPI.Schema.yaml CaBackendCreate.
/// </summary>
public class CaBackendCreateDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("config")]
    public Dictionary<string, object>? Config { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// DTO for updating a CA backend. Matches OpenAPI.Schema.yaml CaBackendUpdate.
/// </summary>
public class CaBackendUpdateDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("config")]
    public Dictionary<string, object>? Config { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool? IsEnabled { get; set; }
}
