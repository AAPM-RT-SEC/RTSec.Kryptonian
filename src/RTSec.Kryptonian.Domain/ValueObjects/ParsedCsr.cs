using System.Security.Cryptography;

namespace RTSec.Kryptonian.Domain.ValueObjects;

/// <summary>
/// Represents a parsed PKCS#10 Certificate Signing Request.
/// </summary>
public class ParsedCsr
{
    /// <summary>
    /// The raw DER-encoded CSR bytes.
    /// </summary>
    public IReadOnlyList<byte> RawData { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Subject Alternative Names (if present).
    /// </summary>
    public IReadOnlyList<string> SubjectAlternativeNames { get; init; } = new List<string>();

    /// <summary>
    /// The signature algorithm used.
    /// </summary>
    public string SignatureAlgorithm { get; init; } = string.Empty;
}
