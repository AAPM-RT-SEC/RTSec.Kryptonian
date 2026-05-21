namespace RTSec.Kryptonian.Domain.Enums;

/// <summary>
/// SMTP authentication modes. Hospital relays commonly use either anonymous IP-allowlisting
/// or NTLM against Exchange; Basic covers everything else.
/// </summary>
public enum SmtpAuthMode
{
    /// <summary>Anonymous relay (common for IP-allowlisted internal SMTP gateways).</summary>
    None = 0,

    /// <summary>SMTP AUTH PLAIN/LOGIN with username and password.</summary>
    Basic = 1,

    /// <summary>NTLM authentication, typical for on-prem Exchange.</summary>
    Ntlm = 2
}
