using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;
using BcX509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace RTSec.Kryptonian.Infrastructure.Crypto;

/// <summary>
/// Service for PKCS#10/PKCS#7 encoding and decoding per EST protocol (RFC 7030).
/// Uses System.Security.Cryptography where possible, BouncyCastle for PKCS#7 degenerate SignedData.
/// </summary>
public class PkcsService : IPkcsService
{
    private readonly ILogger<PkcsService> _logger;

    private const string PemCsrHeader = "-----BEGIN CERTIFICATE REQUEST-----";
    private const string PemCsrFooter = "-----END CERTIFICATE REQUEST-----";
    private const string PemNewCsrHeader = "-----BEGIN NEW CERTIFICATE REQUEST-----";
    private const string PemNewCsrFooter = "-----END NEW CERTIFICATE REQUEST-----";

    public PkcsService(ILogger<PkcsService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ParsedCsr ParsePkcs10(byte[] csrBytes)
    {
        ArgumentNullException.ThrowIfNull(csrBytes);

        if (csrBytes.Length == 0)
            throw new ArgumentException("CSR bytes cannot be empty", nameof(csrBytes));

        _logger.LogDebug("Parsing PKCS#10 CSR, input length: {Length} bytes", csrBytes.Length);

        // Determine format and get DER bytes
        var derBytes = ConvertToDer(csrBytes);

        // Parse using BouncyCastle for full CSR access
        var pkcs10 = new Pkcs10CertificationRequest(derBytes);

        if (!pkcs10.Verify())
        {
            _logger.LogWarning("CSR signature verification failed");
            throw new CryptographicException("CSR signature verification failed");
        }

        var info = pkcs10.GetCertificationRequestInfo();
        var publicKeyInfo = info.SubjectPublicKeyInfo;
        var publicKey = PublicKeyFactory.CreateKey(publicKeyInfo);

        var parsed = new ParsedCsr
        {
            RawData = derBytes,
            SubjectDn = info.Subject.ToString(),
            PublicKey = ConvertToAsymmetricAlgorithm(publicKey),
            PublicKeyAlgorithm = GetAlgorithmName(publicKey),
            KeySize = GetKeySize(publicKey),
            SignatureAlgorithm = pkcs10.SignatureAlgorithm.Algorithm.Id,
            SubjectAlternativeNames = ExtractSans(info)
        };

        _logger.LogDebug("Parsed CSR: Subject={SubjectDn}, Algorithm={Algorithm}, KeySize={KeySize}",
            parsed.SubjectDn, parsed.PublicKeyAlgorithm, parsed.KeySize);

        return parsed;
    }

    /// <inheritdoc />
    public async Task<byte[]> DecodeEstRequestBodyAsync(Stream body, string? contentTransferEncoding, int maxSize = 65536, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Maximum size must be positive");

        // Read with size limit to prevent DoS
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int totalRead = 0;
        int bytesRead;

        while ((bytesRead = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxSize)
            {
                _logger.LogWarning("EST request body exceeded maximum size of {MaxSize} bytes", maxSize);
                throw new InvalidOperationException($"Request body exceeds maximum allowed size of {maxSize} bytes");
            }

            await ms.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }

        var bodyBytes = ms.ToArray();

        if (bodyBytes.Length == 0)
            throw new ArgumentException("Request body cannot be empty", nameof(body));

        _logger.LogDebug("Decoding EST request body, length: {Length}, encoding: {Encoding}",
            bodyBytes.Length, contentTransferEncoding ?? "none");

        // EST protocol specifies base64 encoding
        if (string.Equals(contentTransferEncoding, "base64", StringComparison.OrdinalIgnoreCase))
        {
            var base64String = Encoding.ASCII.GetString(bodyBytes).Trim();
            return Convert.FromBase64String(base64String);
        }

        // If no encoding specified, try to detect base64
        var text = Encoding.ASCII.GetString(bodyBytes).Trim();
        if (IsBase64String(text))
        {
            _logger.LogDebug("Auto-detected base64 encoding");
            return Convert.FromBase64String(text);
        }

        // Return as-is (assumed DER)
        return bodyBytes;
    }

    /// <inheritdoc />
    public byte[] EncodeToPkcs7(X509Certificate2[] certs)
    {
        ArgumentNullException.ThrowIfNull(certs);

        if (certs.Length == 0)
            throw new ArgumentException("Certificate array cannot be empty", nameof(certs));

        _logger.LogDebug("Encoding {Count} certificates to PKCS#7 degenerate SignedData", certs.Length);

        // Convert .NET certs to BouncyCastle certs
        var parser = new X509CertificateParser();
        var bcCerts = certs.Select(c => parser.ReadCertificate(c.RawData)).ToList();

        // Create a degenerate SignedData (certs-only, no signers)
        var certStore = CollectionUtilities.CreateStore(bcCerts);

        var generator = new CmsSignedDataGenerator();
        generator.AddCertificates(certStore);

        // Generate with no content (degenerate)
        var signedData = generator.Generate(new CmsProcessableByteArray(Array.Empty<byte>()), false);

        var encoded = signedData.GetEncoded();

        _logger.LogDebug("Generated PKCS#7 degenerate SignedData, length: {Length} bytes", encoded.Length);

        return encoded;
    }

    /// <inheritdoc />
    public byte[] EncodeEstResponseBody(byte[] pkcs7)
    {
        ArgumentNullException.ThrowIfNull(pkcs7);

        // EST response is base64-encoded
        var base64 = Convert.ToBase64String(pkcs7);
        return Encoding.ASCII.GetBytes(base64);
    }

    /// <inheritdoc />
    public string ExportToPem(X509Certificate2 cert)
    {
        ArgumentNullException.ThrowIfNull(cert);

        var base64 = Convert.ToBase64String(cert.RawData);
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN CERTIFICATE-----");

        // Break into 64-character lines
        for (var i = 0; i < base64.Length; i += 64)
        {
            var length = Math.Min(64, base64.Length - i);
            sb.AppendLine(base64.Substring(i, length));
        }

        sb.AppendLine("-----END CERTIFICATE-----");
        return sb.ToString();
    }

    /// <inheritdoc />
    public bool ValidateCsrSignature(ParsedCsr csr)
    {
        ArgumentNullException.ThrowIfNull(csr);

        if (csr.RawData == null || csr.RawData.Count == 0)
            return false;

        try
        {
            var pkcs10 = new Pkcs10CertificationRequest(csr.RawData.ToArray());
            return pkcs10.Verify();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CSR signature validation failed");
            return false;
        }
    }

    private byte[] ConvertToDer(byte[] input)
    {
        var text = Encoding.ASCII.GetString(input).Trim();

        // Check for PEM format
        if (text.StartsWith(PemCsrHeader) || text.StartsWith(PemNewCsrHeader))
        {
            _logger.LogDebug("Input is PEM format, extracting DER");
            return ExtractDerFromPem(text);
        }

        // Check if it's already base64 (without PEM headers)
        if (IsBase64String(text) && !IsDerFormat(input))
        {
            _logger.LogDebug("Input appears to be raw base64, decoding");
            return Convert.FromBase64String(text);
        }

        // Assume DER format
        _logger.LogDebug("Input appears to be DER format");
        return input;
    }

    private static byte[] ExtractDerFromPem(string pem)
    {
        // Remove headers and whitespace
        var lines = pem.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !l.StartsWith("-----"))
            .ToList();

        var base64 = string.Join("", lines);
        return Convert.FromBase64String(base64);
    }

    private static bool IsBase64String(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        // Quick check: base64 strings contain only valid characters
        text = text.Replace("\r", "").Replace("\n", "").Replace(" ", "");

        if (text.Length % 4 != 0)
            return false;

        try
        {
            Convert.FromBase64String(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDerFormat(byte[] data)
    {
        // DER-encoded ASN.1 SEQUENCE starts with 0x30
        return data.Length > 0 && data[0] == 0x30;
    }

    private static AsymmetricAlgorithm? ConvertToAsymmetricAlgorithm(AsymmetricKeyParameter bcKey)
    {
        if (bcKey is RsaKeyParameters rsaKey)
        {
            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = rsaKey.Modulus.ToByteArrayUnsigned(),
                Exponent = rsaKey.Exponent.ToByteArrayUnsigned()
            });
            return rsa;
        }

        if (bcKey is ECPublicKeyParameters ecKey)
        {
            var ecdsa = ECDsa.Create();
            var q = ecKey.Q;
            var curve = GetECCurve(ecKey.Parameters);

            if (curve.HasValue)
            {
                ecdsa.ImportParameters(new ECParameters
                {
                    Curve = curve.Value,
                    Q = new ECPoint
                    {
                        X = q.AffineXCoord.GetEncoded(),
                        Y = q.AffineYCoord.GetEncoded()
                    }
                });
            }

            return ecdsa;
        }

        return null;
    }

    private static ECCurve? GetECCurve(ECDomainParameters parameters)
    {
        // Map common curves by OID or curve size
        var curveSize = parameters.N.BitLength;

        return curveSize switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => null
        };
    }

    private static string GetAlgorithmName(AsymmetricKeyParameter key)
    {
        return key switch
        {
            RsaKeyParameters => "RSA",
            ECPublicKeyParameters => "ECDSA",
            _ => "Unknown"
        };
    }

    private static int GetKeySize(AsymmetricKeyParameter key)
    {
        return key switch
        {
            RsaKeyParameters rsa => rsa.Modulus.BitLength,
            ECPublicKeyParameters ec => ec.Parameters.N.BitLength,
            _ => 0
        };
    }

    private List<string> ExtractSans(CertificationRequestInfo info)
    {
        var sans = new List<string>();

        try
        {
            var attributes = info.Attributes;
            if (attributes == null)
                return sans;

            foreach (var attr in attributes)
            {
                if (attr is not DerSequence seq || seq.Count < 2)
                    continue;

                var attrOid = seq[0] as DerObjectIdentifier;
                if (attrOid?.Id != PkcsObjectIdentifiers.Pkcs9AtExtensionRequest.Id)
                    continue;

                var attrValues = seq[1] as DerSet;
                if (attrValues == null || attrValues.Count == 0)
                    continue;

                var extensions = X509Extensions.GetInstance(attrValues[0]);
                var sanExtension = extensions.GetExtension(X509Extensions.SubjectAlternativeName);

                if (sanExtension == null)
                    continue;

                var sanNames = GeneralNames.GetInstance(sanExtension.GetParsedValue());
                foreach (var name in sanNames.GetNames())
                {
                    var sanValue = name.TagNo switch
                    {
                        GeneralName.DnsName => $"DNS:{name.Name}",
                        GeneralName.IPAddress => $"IP:{FormatIpAddress((DerOctetString)name.Name)}",
                        GeneralName.Rfc822Name => $"email:{name.Name}",
                        GeneralName.UniformResourceIdentifier => $"URI:{name.Name}",
                        _ => null
                    };

                    if (sanValue != null)
                        sans.Add(sanValue);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract SANs from CSR attributes");
        }

        return sans;
    }

    private static string FormatIpAddress(DerOctetString octets)
    {
        var bytes = octets.GetOctets();
        // Use IPAddress.ToString() for both IPv4 (4 bytes) and IPv6 (16 bytes)
        return new System.Net.IPAddress(bytes).ToString();
    }
}
