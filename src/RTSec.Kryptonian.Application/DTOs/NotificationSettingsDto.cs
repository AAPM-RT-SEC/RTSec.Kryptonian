using System.Text.Json.Serialization;

namespace RTSec.Kryptonian.Application.DTOs;

public class NotificationSettingsDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("smtpHost")] public string SmtpHost { get; set; } = string.Empty;
    [JsonPropertyName("smtpPort")] public int SmtpPort { get; set; }
    [JsonPropertyName("tlsMode")] public string TlsMode { get; set; } = "starttls";
    [JsonPropertyName("authMode")] public string AuthMode { get; set; } = "none";
    [JsonPropertyName("username")] public string? Username { get; set; }

    /// <summary>True when a password is stored. The plaintext is never returned.</summary>
    [JsonPropertyName("hasPassword")] public bool HasPassword { get; set; }

    [JsonPropertyName("fromAddress")] public string FromAddress { get; set; } = string.Empty;
    [JsonPropertyName("fromDisplayName")] public string? FromDisplayName { get; set; }
    [JsonPropertyName("trustServerCertificate")] public bool TrustServerCertificate { get; set; }
    [JsonPropertyName("notifyOnEnrollmentRejected")] public bool NotifyOnEnrollmentRejected { get; set; }
    [JsonPropertyName("notifyOnCertificateNearExpiry")] public bool NotifyOnCertificateNearExpiry { get; set; }
    [JsonPropertyName("expiryWarningDays")] public int ExpiryWarningDays { get; set; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; set; }
}

public class NotificationSettingsUpdateDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("smtpHost")] public string SmtpHost { get; set; } = string.Empty;
    [JsonPropertyName("smtpPort")] public int SmtpPort { get; set; } = 587;
    [JsonPropertyName("tlsMode")] public string TlsMode { get; set; } = "starttls";
    [JsonPropertyName("authMode")] public string AuthMode { get; set; } = "none";
    [JsonPropertyName("username")] public string? Username { get; set; }

    /// <summary>
    /// Optional. When null, the existing password is preserved. When empty string, the
    /// password is cleared. When non-empty, the new value is encrypted and stored.
    /// </summary>
    [JsonPropertyName("password")] public string? Password { get; set; }

    [JsonPropertyName("fromAddress")] public string FromAddress { get; set; } = string.Empty;
    [JsonPropertyName("fromDisplayName")] public string? FromDisplayName { get; set; }
    [JsonPropertyName("trustServerCertificate")] public bool TrustServerCertificate { get; set; }
    [JsonPropertyName("notifyOnEnrollmentRejected")] public bool NotifyOnEnrollmentRejected { get; set; }
    [JsonPropertyName("notifyOnCertificateNearExpiry")] public bool NotifyOnCertificateNearExpiry { get; set; }
    [JsonPropertyName("expiryWarningDays")] public int ExpiryWarningDays { get; set; }
}

public class NotificationRecipientDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    [JsonPropertyName("notifyOnEnrollmentRejected")] public bool NotifyOnEnrollmentRejected { get; set; }
    [JsonPropertyName("notifyOnCertificateNearExpiry")] public bool NotifyOnCertificateNearExpiry { get; set; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; set; }
}

public class NotificationRecipientUpsertDto
{
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    [JsonPropertyName("notifyOnEnrollmentRejected")] public bool NotifyOnEnrollmentRejected { get; set; }
    [JsonPropertyName("notifyOnCertificateNearExpiry")] public bool NotifyOnCertificateNearExpiry { get; set; }
}

public class NotificationTestRequestDto
{
    /// <summary>
    /// Settings to test. Treated as a draft — the supplied password (if any) is used
    /// directly; if Password is null and HasPassword is true, the stored password is used.
    /// </summary>
    [JsonPropertyName("settings")] public NotificationSettingsUpdateDto Settings { get; set; } = new();

    [JsonPropertyName("recipientEmail")] public string RecipientEmail { get; set; } = string.Empty;
}
