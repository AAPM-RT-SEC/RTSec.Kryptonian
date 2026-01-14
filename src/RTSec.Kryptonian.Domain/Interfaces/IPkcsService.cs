using System.Security.Cryptography.X509Certificates;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Domain.Interfaces;

/// <summary>
/// Service for PKCS#10/PKCS#7 encoding and decoding (EST wire format).
/// </summary>
public interface IPkcsService
{
    /// <summary>
    /// Parses a PKCS#10 CSR from bytes (handles base64, PEM, and DER formats).
    /// </summary>
    ParsedCsr ParsePkcs10(byte[] csrBytes);

    /// <summary>
    /// Decodes EST request body (handles Content-Transfer-Encoding: base64).
    /// </summary>
    /// <param name="body">The request body stream.</param>
    /// <param name="contentTransferEncoding">The Content-Transfer-Encoding header value.</param>
    /// <param name="maxSize">Maximum allowed body size in bytes (default 64KB).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="InvalidOperationException">Thrown when body exceeds maxSize.</exception>
    Task<byte[]> DecodeEstRequestBodyAsync(Stream body, string? contentTransferEncoding, int maxSize = 65536, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encodes certificates to PKCS#7 degenerate SignedData (certs-only).
    /// </summary>
    byte[] EncodeToPkcs7(X509Certificate2[] certs);

    /// <summary>
    /// Encodes PKCS#7 data to base64 for EST response.
    /// </summary>
    byte[] EncodeEstResponseBody(byte[] pkcs7);

    /// <summary>
    /// Exports a certificate to PEM format.
    /// </summary>
    string ExportToPem(X509Certificate2 cert);

    /// <summary>
    /// Validates the signature on a CSR.
    /// </summary>
    bool ValidateCsrSignature(ParsedCsr csr);
}
