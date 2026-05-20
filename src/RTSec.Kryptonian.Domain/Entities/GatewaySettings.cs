namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// Singleton gateway-wide configuration covering device-registration defaults.
/// </summary>
public class GatewaySettings : BaseEntity
{
    public const int MinCertificateLifetimeHours = 24;
    public const int MaxCertificateLifetimeHours = 24 * 365 * 2;
    public const int DefaultCertificateLifetimeHoursDefault = 24;

    /// <summary>
    /// Default certificate lifetime in hours applied to newly issued device certificates
    /// when an EST profile does not specify its own validity. Range: 24 hours to 2 years.
    /// </summary>
    public int DefaultCertificateLifetimeHours { get; set; } = DefaultCertificateLifetimeHoursDefault;
}
