using System.Text.Json.Serialization;

namespace RTSec.Kryptonian.Application.DTOs;

public class DeviceDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("subjectCommonName")]
    public string SubjectCommonName { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("hasActivationCode")]
    public bool HasActivationCode { get; set; }

    [JsonPropertyName("activationCodeExpiresAt")]
    public DateTime? ActivationCodeExpiresAt { get; set; }

    [JsonPropertyName("activationCodeUsedAt")]
    public DateTime? ActivationCodeUsedAt { get; set; }

    [JsonPropertyName("approvedAt")]
    public DateTime? ApprovedAt { get; set; }

    [JsonPropertyName("removedAt")]
    public DateTime? RemovedAt { get; set; }

    [JsonPropertyName("lastCertificateId")]
    public string? LastCertificateId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class DeviceCreateDto
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("subjectCommonName")]
    public string SubjectCommonName { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }
}

public class DeviceApprovalRequestDto
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("subjectCommonName")]
    public string SubjectCommonName { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }
}

public class DeviceApprovalRequestResponseDto
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("subjectCommonName")]
    public string SubjectCommonName { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class DeviceActivationCodeCreateDto
{
    [JsonPropertyName("validForMinutes")]
    public int? ValidForMinutes { get; set; }
}

public class DeviceActivationCodeDto
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("subjectCommonName")]
    public string SubjectCommonName { get; set; } = string.Empty;

    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("activationCode")]
    public string ActivationCode { get; set; } = string.Empty;

    [JsonPropertyName("qrPayload")]
    public string QrPayload { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}

public class CertificateDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("subjectDn")]
    public string SubjectDn { get; set; } = string.Empty;

    [JsonPropertyName("issuerDn")]
    public string IssuerDn { get; set; } = string.Empty;

    [JsonPropertyName("thumbprint")]
    public string Thumbprint { get; set; } = string.Empty;

    [JsonPropertyName("certificatePem")]
    public string CertificatePem { get; set; } = string.Empty;

    [JsonPropertyName("certificateDerBase64")]
    public string? CertificateDerBase64 { get; set; }

    [JsonPropertyName("caBackendId")]
    public string? CaBackendId { get; set; }

    [JsonPropertyName("caBackendType")]
    public string? CaBackendType { get; set; }

    [JsonPropertyName("gatewayOid")]
    public string? GatewayOid { get; set; }

    [JsonPropertyName("notBefore")]
    public DateTime NotBefore { get; set; }

    [JsonPropertyName("notAfter")]
    public DateTime NotAfter { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class DemoEnrollResponseDto
{
    [JsonPropertyName("device")]
    public DeviceDto Device { get; set; } = new();

    [JsonPropertyName("certificate")]
    public CertificateDto Certificate { get; set; } = new();

    [JsonPropertyName("issuedByActiveBackend")]
    public CaBackendDto IssuedByActiveBackend { get; set; } = new();
}
