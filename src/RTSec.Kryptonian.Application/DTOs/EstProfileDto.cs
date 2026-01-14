using System.Text.Json.Serialization;

namespace RTSec.Kryptonian.Application.DTOs;

/// <summary>
/// DTO for EST profile response. Matches OpenAPI.Schema.yaml EstProfile.
/// </summary>
public class EstProfileDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("hostnames")]
    public List<string> Hostnames { get; set; } = new();

    /// <summary>
    /// How hostnames should be matched: "exact", "suffix", or "wildcard".
    /// </summary>
    [JsonPropertyName("hostnameMatchType")]
    public string HostnameMatchType { get; set; } = "exact";

    /// <summary>
    /// When using wildcard match type, restricts wildcards to this suffix (e.g., ".example.com").
    /// </summary>
    [JsonPropertyName("allowedWildcardSuffix")]
    public string? AllowedWildcardSuffix { get; set; }

    [JsonPropertyName("pathPrefix")]
    public string PathPrefix { get; set; } = "/.well-known/est";

    [JsonPropertyName("caBackendId")]
    public string CaBackendId { get; set; } = string.Empty;

    [JsonPropertyName("certificateTemplate")]
    public string? CertificateTemplate { get; set; }

    [JsonPropertyName("allowedKeyUsages")]
    public List<string> AllowedKeyUsages { get; set; } = new();

    [JsonPropertyName("validityDays")]
    public int ValidityDays { get; set; }

    [JsonPropertyName("requireClientCertificate")]
    public bool RequireClientCertificate { get; set; }

    /// <summary>
    /// Whether to validate client certificate chain against trusted CAs.
    /// </summary>
    [JsonPropertyName("validateClientCertificateChain")]
    public bool ValidateClientCertificateChain { get; set; }

    /// <summary>
    /// List of trusted CA certificate thumbprints (SHA-256) for client cert validation.
    /// </summary>
    [JsonPropertyName("trustedClientCaThumbprints")]
    public List<string> TrustedClientCaThumbprints { get; set; } = new();

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating an EST profile. Matches OpenAPI.Schema.yaml EstProfileCreate.
/// </summary>
public class EstProfileCreateDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("hostnames")]
    public List<string> Hostnames { get; set; } = new();

    /// <summary>
    /// How hostnames should be matched: "exact", "suffix", or "wildcard". Default is "exact".
    /// </summary>
    [JsonPropertyName("hostnameMatchType")]
    public string HostnameMatchType { get; set; } = "exact";

    /// <summary>
    /// When using wildcard match type, restricts wildcards to this suffix (e.g., ".example.com").
    /// </summary>
    [JsonPropertyName("allowedWildcardSuffix")]
    public string? AllowedWildcardSuffix { get; set; }

    [JsonPropertyName("pathPrefix")]
    public string PathPrefix { get; set; } = "/.well-known/est";

    [JsonPropertyName("caBackendId")]
    public string CaBackendId { get; set; } = string.Empty;

    [JsonPropertyName("certificateTemplate")]
    public string? CertificateTemplate { get; set; }

    [JsonPropertyName("allowedKeyUsages")]
    public List<string>? AllowedKeyUsages { get; set; }

    [JsonPropertyName("validityDays")]
    public int ValidityDays { get; set; } = 365;

    [JsonPropertyName("requireClientCertificate")]
    public bool RequireClientCertificate { get; set; } = true;

    /// <summary>
    /// Whether to validate client certificate chain against trusted CAs. Default is false.
    /// </summary>
    [JsonPropertyName("validateClientCertificateChain")]
    public bool ValidateClientCertificateChain { get; set; } = false;

    /// <summary>
    /// List of trusted CA certificate thumbprints (SHA-256) for client cert validation.
    /// Required when validateClientCertificateChain is true.
    /// </summary>
    [JsonPropertyName("trustedClientCaThumbprints")]
    public List<string>? TrustedClientCaThumbprints { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// DTO for updating an EST profile. Matches OpenAPI.Schema.yaml EstProfileUpdate.
/// </summary>
public class EstProfileUpdateDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hostnames")]
    public List<string>? Hostnames { get; set; }

    /// <summary>
    /// How hostnames should be matched: "exact", "suffix", or "wildcard".
    /// </summary>
    [JsonPropertyName("hostnameMatchType")]
    public string? HostnameMatchType { get; set; }

    /// <summary>
    /// When using wildcard match type, restricts wildcards to this suffix (e.g., ".example.com").
    /// </summary>
    [JsonPropertyName("allowedWildcardSuffix")]
    public string? AllowedWildcardSuffix { get; set; }

    [JsonPropertyName("pathPrefix")]
    public string? PathPrefix { get; set; }

    [JsonPropertyName("caBackendId")]
    public string? CaBackendId { get; set; }

    [JsonPropertyName("certificateTemplate")]
    public string? CertificateTemplate { get; set; }

    [JsonPropertyName("allowedKeyUsages")]
    public List<string>? AllowedKeyUsages { get; set; }

    [JsonPropertyName("validityDays")]
    public int? ValidityDays { get; set; }

    [JsonPropertyName("requireClientCertificate")]
    public bool? RequireClientCertificate { get; set; }

    /// <summary>
    /// Whether to validate client certificate chain against trusted CAs.
    /// </summary>
    [JsonPropertyName("validateClientCertificateChain")]
    public bool? ValidateClientCertificateChain { get; set; }

    /// <summary>
    /// List of trusted CA certificate thumbprints (SHA-256) for client cert validation.
    /// </summary>
    [JsonPropertyName("trustedClientCaThumbprints")]
    public List<string>? TrustedClientCaThumbprints { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool? IsEnabled { get; set; }
}
