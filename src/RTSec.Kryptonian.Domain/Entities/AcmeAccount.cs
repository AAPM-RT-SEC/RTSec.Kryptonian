namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>
/// Represents an ACME account used for certificate issuance.
/// The account key is stored encrypted in the database.
/// </summary>
public class AcmeAccount : BaseEntity
{
    /// <summary>
    /// The ACME directory URL this account is registered with.
    /// e.g., "https://acme-v02.api.letsencrypt.org/directory"
    /// </summary>
    public Uri DirectoryUrl { get; set; } = new Uri("https://acme-v02.api.letsencrypt.org/directory");

    /// <summary>
    /// The email address associated with this account.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The ACME account URL returned by the ACME server after registration.
    /// </summary>
    public Uri? AccountUrl { get; set; }

    /// <summary>
    /// The account private key in PEM format, encrypted.
    /// </summary>
    public string EncryptedPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the terms of service have been accepted.
    /// </summary>
    public bool TermsOfServiceAccepted { get; set; }

    /// <summary>
    /// Whether this account is active and should be used.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// External Account Binding (EAB) Key Identifier (for CAs that require it like ZeroSSL).
    /// </summary>
    public string? EabKeyId { get; set; }

    /// <summary>
    /// External Account Binding (EAB) HMAC Key (encrypted, for CAs that require it).
    /// </summary>
    public string? EncryptedEabHmacKey { get; set; }
}
