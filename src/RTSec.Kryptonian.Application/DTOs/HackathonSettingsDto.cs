using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTSec.Kryptonian.Application.DTOs;

public class HackathonSettingsDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("harnessBaseUrl")]
    public string HarnessBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("teamToken")]
    public string TeamToken { get; set; } = string.Empty;

    [JsonPropertyName("dimseHost")]
    public string DimseHost { get; set; } = string.Empty;

    [JsonPropertyName("dimseTlsPort")]
    public int DimseTlsPort { get; set; }

    [JsonPropertyName("orthancDimsePort")]
    public int OrthancDimsePort { get; set; }

    [JsonPropertyName("dicomWebBaseUrl")]
    public string DicomWebBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("calledAeTitle")]
    public string CalledAeTitle { get; set; } = string.Empty;

    [JsonPropertyName("bridgeAeTitle")]
    public string BridgeAeTitle { get; set; } = string.Empty;

    [JsonPropertyName("bridgeListenPort")]
    public int BridgeListenPort { get; set; }

    [JsonPropertyName("trustedProxyCertificateThumbprint")]
    public string? TrustedProxyCertificateThumbprint { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class HackathonSettingsUpdateDto
{
    [JsonPropertyName("harnessBaseUrl")]
    public string HarnessBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("teamToken")]
    public string TeamToken { get; set; } = string.Empty;

    [JsonPropertyName("dimseHost")]
    public string DimseHost { get; set; } = string.Empty;

    [JsonPropertyName("dimseTlsPort")]
    public int DimseTlsPort { get; set; }

    [JsonPropertyName("orthancDimsePort")]
    public int OrthancDimsePort { get; set; }

    [JsonPropertyName("dicomWebBaseUrl")]
    public string DicomWebBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("calledAeTitle")]
    public string CalledAeTitle { get; set; } = string.Empty;

    [JsonPropertyName("bridgeAeTitle")]
    public string BridgeAeTitle { get; set; } = string.Empty;

    [JsonPropertyName("bridgeListenPort")]
    public int BridgeListenPort { get; set; }

    [JsonPropertyName("trustedProxyCertificateThumbprint")]
    public string? TrustedProxyCertificateThumbprint { get; set; }
}

public class HarnessScoreboardSnapshotDto
{
    [JsonPropertyName("fetchedAt")]
    public DateTime FetchedAt { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public JsonElement Body { get; set; }
}
