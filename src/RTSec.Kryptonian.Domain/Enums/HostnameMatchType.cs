namespace RTSec.Kryptonian.Domain.Enums;

/// <summary>
/// Defines how hostnames are matched for EST profile routing.
/// </summary>
public enum HostnameMatchType
{
    /// <summary>
    /// Exact hostname match (e.g., "est.example.com" matches only "est.example.com").
    /// </summary>
    Exact,

    /// <summary>
    /// Suffix match for subdomains (e.g., ".example.com" matches "a.example.com", "b.example.com").
    /// The leading dot is required and the suffix must be at least a second-level domain.
    /// </summary>
    Suffix,

    /// <summary>
    /// Wildcard match for all hostnames. Use with caution - typically restricted by AllowedWildcardSuffix.
    /// </summary>
    Wildcard
}
