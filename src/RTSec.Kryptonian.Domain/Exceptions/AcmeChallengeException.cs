namespace RTSec.Kryptonian.Domain.Exceptions;

/// <summary>
/// Exception thrown when ACME challenge validation fails.
/// </summary>
public class AcmeChallengeException : Exception
{
    /// <summary>
    /// The domain being validated.
    /// </summary>
    public string Domain { get; }

    /// <summary>
    /// The challenge type (http-01 or dns-01).
    /// </summary>
    public string ChallengeType { get; }

    /// <summary>
    /// Whether this is a transient error that may succeed on retry.
    /// </summary>
    public bool IsTransient { get; }

    /// <summary>
    /// Suggested retry delay in seconds, if applicable.
    /// </summary>
    public int? RetryAfterSeconds { get; }

    /// <summary>
    /// ACME error type URI if available (e.g., "urn:ietf:params:acme:error:unauthorized").
    /// </summary>
    public string? AcmeErrorType { get; }

    public AcmeChallengeException(
        string domain,
        string challengeType,
        string message,
        bool isTransient = false,
        int? retryAfterSeconds = null,
        string? acmeErrorType = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Domain = domain;
        ChallengeType = challengeType;
        IsTransient = isTransient;
        RetryAfterSeconds = retryAfterSeconds;
        AcmeErrorType = acmeErrorType;
    }
}
