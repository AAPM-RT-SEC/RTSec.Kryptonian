using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// Represents a Certificate Authority backend configuration.
/// Aligned with OpenAPI.Schema.yaml CaBackend schema.
/// </summary>
public class CaBackend : BaseEntity
{
    /// <summary>
    /// Display name for the CA backend.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Backend type: "adcs", "ejbca", "cfssl", "selfsigned", "acme".
    /// </summary>
    public CaBackendType Type { get; set; }

    /// <summary>
    /// URL for the CA backend (optional for self-signed).
    /// </summary>
    public Uri? Url { get; set; }

    /// <summary>
    /// Backend-specific configuration stored as JSON.
    /// Validated per backend type.
    /// </summary>
    public Dictionary<string, object> Config { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Whether this backend is enabled for use.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    // Navigation properties
    public ICollection<EstProfile> EstProfiles { get; } = new List<EstProfile>();
}
