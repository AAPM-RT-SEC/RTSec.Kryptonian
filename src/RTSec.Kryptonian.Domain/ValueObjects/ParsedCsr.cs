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
    /// Subject Distinguished Name from the CSR.
    /// </summary>
    public string SubjectDn { get; init; } = string.Empty;

    /// <summary>
    /// The public key from the CSR.
    /// </summary>
    public AsymmetricAlgorithm? PublicKey { get; init; }

    /// <summary>
    /// The public key algorithm (RSA, ECDSA, etc.).
    /// </summary>
    public string PublicKeyAlgorithm { get; init; } = string.Empty;

    /// <summary>
    /// The key size in bits.
    /// </summary>
    public int KeySize { get; init; }

    /// <summary>
    /// Subject Alternative Names (if present).
    /// </summary>
    public IReadOnlyList<string> SubjectAlternativeNames { get; init; } = new List<string>();

    /// <summary>
    /// The signature algorithm used.
    /// </summary>
    public string SignatureAlgorithm { get; init; } = string.Empty;
}
