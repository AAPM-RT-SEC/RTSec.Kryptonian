using System.Security.Cryptography.X509Certificates;

namespace RTSec.Kryptonian.Domain.ValueObjects;

/// <summary>
/// Result of a certificate issuance operation.
/// </summary>
public class CertificateIssuanceResult
{
    /// <summary>
    /// Whether the issuance was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The issued certificate (if successful).
    /// </summary>
    public X509Certificate2? Certificate { get; init; }

    /// <summary>
    /// The full certificate chain including intermediates and root.
    /// </summary>
    public IReadOnlyList<X509Certificate2>? CertificateChain { get; init; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether the request is pending (async issuance).
    /// </summary>
    public bool IsPending { get; init; }

    /// <summary>
    /// Retry-After value in seconds (if pending).
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    public static CertificateIssuanceResult Successful(X509Certificate2 cert, X509Certificate2[] chain)
        => new()
        {
            Success = true,
            Certificate = cert,
            CertificateChain = chain
        };

    public static CertificateIssuanceResult Failed(string error)
        => new()
        {
            Success = false,
            ErrorMessage = error
        };

    public static CertificateIssuanceResult Pending(int retryAfterSeconds)
        => new()
        {
            Success = false,
            IsPending = true,
            RetryAfterSeconds = retryAfterSeconds
        };
}
