namespace RTSec.Kryptonian.Domain.Enums;

/// <summary>
/// TLS modes for SMTP connections. Chosen for on-prem SMTP relays which may use plain,
/// STARTTLS, or implicit TLS depending on hospital infrastructure.
/// </summary>
public enum SmtpTlsMode
{
    /// <summary>No transport encryption. Only safe on isolated networks.</summary>
    None = 0,

    /// <summary>Connect plaintext then upgrade via STARTTLS (typical port 587).</summary>
    StartTls = 1,

    /// <summary>TLS from the first byte (typical port 465).</summary>
    Implicit = 2
}
