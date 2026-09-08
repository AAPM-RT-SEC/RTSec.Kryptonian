using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// Represents an EST profile configuration.
/// Aligned with OpenAPI.Schema.yaml EstProfile schema.
/// </summary>
public class EstProfile : BaseEntity
{
    /// <summary>
    /// Display name for the EST profile.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path prefix for EST endpoints, usually "/.well-known/est".
    /// </summary>
    public string PathPrefix { get; set; } = "/.well-known/est";

    /// <summary>
    /// Hostnames this profile responds to.
    /// For Exact match: full hostname (e.g., "est.example.com")
    /// For Suffix match: suffix with leading dot (e.g., ".example.com")
    /// For Wildcard: use "*" (restricted by AllowedWildcardSuffix if set)
    /// </summary>
    public List<string> Hostnames { get; } = new List<string>();

    /// <summary>
    /// How hostnames should be matched. Default is Exact for security.
    /// </summary>
    public HostnameMatchType HostnameMatchType { get; set; } = HostnameMatchType.Exact;

    /// <summary>
    /// When using Wildcard match type, restricts wildcards to this suffix.
    /// E.g., ".example.com" means wildcard only matches *.example.com subdomains.
    /// Null means unrestricted wildcard (use with extreme caution).
    /// </summary>
    public string? AllowedWildcardSuffix { get; set; }

    /// <summary>
    /// Reference to the CA backend used for this profile.
    /// </summary>
    public Guid CaBackendId { get; set; }

    /// <summary>
    /// CA-specific template or profile name.
    /// </summary>
    public string? CertificateTemplate { get; set; }

    /// <summary>
    /// Allowed key usages for issued certificates.
    /// </summary>
    public List<string> AllowedKeyUsages { get; } = new List<string>();

    /// <summary>
    /// Default validity period in days for issued certificates.
    /// </summary>
    public int ValidityDays { get; set; } = 365;

    /// <summary>
    /// Whether TLS client certificate is required for enrollment.
    /// </summary>
    public bool RequireClientCertificate { get; set; } = true;

    /// <summary>
    /// Whether to validate client certificate chain against trusted CAs.
    /// When true, client certs must chain to a trusted issuer configured in TrustedClientCaThumbprints.
    /// Declared as a property (not a field) so EF Core maps and persists it.
    /// </summary>
    public bool ValidateClientCertificateChain { get; set; }

    /// <summary>
    /// List of trusted CA certificate thumbprints for client cert validation.
    /// Client certificates must be issued by one of these CAs when
    /// ValidateClientCertificateChain is true.
    ///
    /// Hash algorithm: the current consumer compares these values against
    /// X509Certificate2.GetCertHashString(), which returns a SHA-1 hex digest. So although
    /// these were historically documented as SHA-256, today only legacy SHA-1 thumbprints
    /// actually match. Do not paste a SHA-256 fingerprint here expecting it to work until
    /// the comparison side is changed to hash with SHA-256 explicitly.
    /// </summary>
    public List<string> TrustedClientCaThumbprints { get; } = new List<string>();

    /// <summary>
    /// Whether this profile is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    // Navigation properties
    public CaBackend? CaBackend { get; set; }
    public ICollection<Certificate> Certificates { get; } = new List<Certificate>();
    public ICollection<EnrollmentEvent> EnrollmentEvents { get; } = new List<EnrollmentEvent>();
}
