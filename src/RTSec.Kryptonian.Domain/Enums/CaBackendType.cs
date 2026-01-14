namespace RTSec.Kryptonian.Domain.Enums;

/// <summary>
/// Supported CA backend types.
/// See README.md for implementation status.
/// </summary>
public enum CaBackendType
{
    // Implemented
    SelfSigned,
    Acme,

    // Planned - Enterprise
    Adcs,           // Microsoft Active Directory Certificate Services
    Ejbca,          // Enterprise Java Beans CA
    Cfssl,          // CloudFlare's PKI toolkit
    HashiCorpVault, // HashiCorp Vault PKI secrets engine
    Smallstep,      // Modern open-source CA
    OpenXpki        // Open-source enterprise PKI
}
