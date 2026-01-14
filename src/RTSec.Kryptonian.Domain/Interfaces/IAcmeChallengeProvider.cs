namespace RTSec.Kryptonian.Domain.Interfaces;

/// <summary>
/// Interface for ACME challenge handling.
/// Implementations provide mechanisms to fulfill HTTP-01 or DNS-01 challenges.
/// </summary>
public interface IAcmeChallengeProvider
{
    /// <summary>
    /// The challenge type this provider handles (http-01 or dns-01).
    /// </summary>
    string ChallengeType { get; }

    /// <summary>
    /// Prepares a challenge by setting up the required validation resource.
    /// For HTTP-01: stores the token/key-authorization for the challenge endpoint.
    /// For DNS-01: creates the required TXT record.
    /// </summary>
    /// <param name="domain">The domain being validated.</param>
    /// <param name="token">The challenge token.</param>
    /// <param name="keyAuthorization">The key authorization value.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PrepareAsync(string domain, string token, string keyAuthorization, CancellationToken ct = default);

    /// <summary>
    /// Cleans up challenge resources after validation completes.
    /// </summary>
    /// <param name="domain">The domain being validated.</param>
    /// <param name="token">The challenge token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CleanupAsync(string domain, string token, CancellationToken ct = default);
}
