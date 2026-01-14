namespace RTSec.Kryptonian.Domain.ValueObjects;

/// <summary>
/// Result of an enrollment operation.
/// </summary>
public class EnrollmentResult
{
    /// <summary>
    /// Whether the enrollment was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// PKCS#7 encoded certificate chain (base64 for EST response).
    /// </summary>
    public byte[]? Pkcs7Response { get; init; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// HTTP status code to return.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Whether the request is pending (async issuance).
    /// </summary>
    public bool IsPending { get; init; }

    /// <summary>
    /// Retry-After value in seconds (if pending).
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// The enrollment event ID for tracking.
    /// </summary>
    public Guid? EnrollmentEventId { get; init; }

    public static EnrollmentResult Successful(byte[] pkcs7Response, Guid enrollmentEventId)
        => new()
        {
            Success = true,
            Pkcs7Response = pkcs7Response,
            StatusCode = 200,
            EnrollmentEventId = enrollmentEventId
        };

    public static EnrollmentResult Failed(string error, int statusCode = 500)
        => new()
        {
            Success = false,
            ErrorMessage = error,
            StatusCode = statusCode
        };

    public static EnrollmentResult Pending(int retryAfterSeconds, Guid enrollmentEventId)
        => new()
        {
            Success = false,
            IsPending = true,
            RetryAfterSeconds = retryAfterSeconds,
            StatusCode = 202,
            EnrollmentEventId = enrollmentEventId
        };
}
