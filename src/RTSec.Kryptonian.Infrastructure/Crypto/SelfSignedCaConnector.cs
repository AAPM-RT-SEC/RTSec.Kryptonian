using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;
using BcX509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace RTSec.Kryptonian.Infrastructure.Crypto;

/// <summary>
/// CA connector that signs certificates using a local CA certificate and private key.
/// </summary>
public class SelfSignedCaConnector : ICaConnector
{
    private readonly ILogger<SelfSignedCaConnector> _logger;
    private readonly X509Certificate2 _caCertificate;
    private AsymmetricKeyParameter? _caPrivateKey;
    private readonly BcX509Certificate _bcCaCertificate;
    private readonly string? _crlDistributionPointUrl;

    public CaBackendType Type => CaBackendType.SelfSigned;

    public SelfSignedCaConnector(
        ILogger<SelfSignedCaConnector> logger,
        X509Certificate2 caCertificate,
        string? crlDistributionPointUrl = null)
    {
        _logger = logger;
        _caCertificate = caCertificate ?? throw new ArgumentNullException(nameof(caCertificate));
        _crlDistributionPointUrl = crlDistributionPointUrl;

        if (!_caCertificate.HasPrivateKey)
            throw new ArgumentException("CA certificate must have a private key", nameof(caCertificate));

        // Convert to BouncyCastle format for signing
        var parser = new X509CertificateParser();
        _bcCaCertificate = parser.ReadCertificate(_caCertificate.RawData);

        _logger.LogInformation("SelfSignedCaConnector initialized with CA: {Subject}, Serial: {Serial}",
            _caCertificate.Subject, _caCertificate.SerialNumber);
    }

    /// <inheritdoc />
    public Task<X509Certificate2[]> GetCaCertificatesAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Returning CA certificate chain");

        // Return the CA certificate (and any intermediates if we had them)
        return Task.FromResult(new[] { _caCertificate });
    }

    /// <inheritdoc />
    public Task<CertificateIssuanceResult> IssueCertificateAsync(
        ParsedCsr csr,
        EstProfile profile,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(csr);
        ArgumentNullException.ThrowIfNull(profile);

        _logger.LogInformation("Issuing certificate for subject: {Subject}, profile: {Profile}",
            csr.SubjectDn, profile.Name);

        try
        {
            // Parse the CSR using BouncyCastle
            var pkcs10 = new Pkcs10CertificationRequest(csr.RawData.ToArray());
            var csrInfo = pkcs10.GetCertificationRequestInfo();

            // Generate serial number
            var serialNumber = GenerateSerialNumber();

            // Calculate validity period
            var notBefore = DateTime.UtcNow.AddMinutes(-5); // 5 min clock skew allowance
            var notAfter = DateTime.UtcNow.AddDays(profile.ValidityDays);

            // Create certificate generator
            var certGen = new X509V3CertificateGenerator();

            // Set basic fields
            certGen.SetSerialNumber(serialNumber);
            certGen.SetSubjectDN(csrInfo.Subject);
            certGen.SetIssuerDN(_bcCaCertificate.SubjectDN);
            certGen.SetNotBefore(notBefore);
            certGen.SetNotAfter(notAfter);
            certGen.SetPublicKey(pkcs10.GetPublicKey());

            // Add extensions
            AddCertificateExtensions(certGen, csrInfo, profile);

            // Sign the certificate
            var privateKey = _caPrivateKey ??= ConvertToBouncyCastlePrivateKey(_caCertificate);
            var signatureAlgorithm = DetermineSignatureAlgorithm(privateKey);
            var signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, privateKey);
            var bcCert = certGen.Generate(signatureFactory);

            // Convert to .NET X509Certificate2
            var certBytes = bcCert.GetEncoded();
            var issuedCert = new X509Certificate2(certBytes);

            _logger.LogInformation("Certificate issued: Serial={Serial}, Subject={Subject}, NotAfter={NotAfter}",
                issuedCert.SerialNumber, issuedCert.Subject, issuedCert.NotAfter);

            // Return the certificate chain (issued cert + CA cert)
            return Task.FromResult(CertificateIssuanceResult.Successful(
                issuedCert,
                new[] { issuedCert, _caCertificate }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue certificate for subject: {Subject}", csr.SubjectDn);
            return Task.FromResult(CertificateIssuanceResult.Failed(ex.Message));
        }
    }

    /// <inheritdoc />
    public Task<bool> RevokeCertificateAsync(
        string serial,
        RevocationReason reason,
        CancellationToken ct = default)
    {
        // State-backed CRL updates require all issuer entries and are coordinated by
        // CertificateRevocationService through GenerateCrlAsync.
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<CrlGenerationResult> GenerateCrlAsync(CrlGenerationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issuerFingerprint = Convert.ToHexString(SHA256.HashData(_caCertificate.RawData));
        if (!string.Equals(issuerFingerprint, request.ExpectedIssuerFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CrlGenerationResult.Unsupported("Configured CA certificate does not match the CRL issuer."));
        }

        if (request.CrlNumber <= 0 || request.NextUpdate <= request.ThisUpdate)
        {
            return Task.FromResult(CrlGenerationResult.Unsupported("Invalid CRL timing or number."));
        }

        try
        {
            var builder = new CertificateRevocationListBuilder();
            foreach (var entry in request.Entries)
            {
                builder.AddEntry(
                    Convert.FromHexString(entry.SerialNumber),
                    new DateTimeOffset(entry.RevokedAt.ToUniversalTime()),
                    ToX509Reason(entry.Reason));
            }

            var der = builder.Build(
                _caCertificate,
                new System.Numerics.BigInteger(request.CrlNumber),
                new DateTimeOffset(request.NextUpdate.ToUniversalTime()),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1,
                new DateTimeOffset(request.ThisUpdate.ToUniversalTime()));
            return Task.FromResult(CrlGenerationResult.Successful(issuerFingerprint, der));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build CRL");
            return Task.FromResult(CrlGenerationResult.Unsupported("Local CA could not generate a CRL."));
        }
    }

    /// <inheritdoc />
    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        // For self-signed, we just verify the CA cert is valid and we have the key
        var isValid = _caCertificate != null
            && _caCertificate.HasPrivateKey
            && _caCertificate.NotAfter.ToUniversalTime() > DateTime.UtcNow
            && _caCertificate.NotBefore.ToUniversalTime() < DateTime.UtcNow;

        _logger.LogDebug("Self-signed CA connection test: {Result}", isValid ? "OK" : "FAILED");

        return Task.FromResult(isValid);
    }

    private void AddCertificateExtensions(
        X509V3CertificateGenerator certGen,
        CertificationRequestInfo csrInfo,
        EstProfile profile)
    {
        // Basic Constraints: Not a CA
        certGen.AddExtension(
            X509Extensions.BasicConstraints,
            true,
            new BasicConstraints(false));

        // Key Usage based on profile
        var keyUsage = GetKeyUsage(profile.AllowedKeyUsages.ToList());
        if (keyUsage != 0)
        {
            certGen.AddExtension(
                X509Extensions.KeyUsage,
                true,
                new KeyUsage(keyUsage));
        }

        // Extended Key Usage (if specified)
        var extendedKeyUsages = GetExtendedKeyUsages(profile.AllowedKeyUsages.ToList());
        if (extendedKeyUsages.Count != 0)
        {
            certGen.AddExtension(
                X509Extensions.ExtendedKeyUsage,
                false,
                new ExtendedKeyUsage(extendedKeyUsages));
        }

        // Subject Key Identifier - compute from public key
        var subjectPublicKeyInfo = csrInfo.SubjectPublicKeyInfo;
        var ski = ComputeSubjectKeyIdentifier(subjectPublicKeyInfo);
        certGen.AddExtension(
            X509Extensions.SubjectKeyIdentifier,
            false,
            new SubjectKeyIdentifier(ski));

        // Authority Key Identifier - compute from CA's public key
        var akiBuilder = new AuthorityKeyIdentifierStructure(_bcCaCertificate);
        certGen.AddExtension(
            X509Extensions.AuthorityKeyIdentifier,
            false,
            akiBuilder);

        // Copy SANs from CSR if present
        CopySansFromCsr(certGen, csrInfo);

        if (!string.IsNullOrWhiteSpace(_crlDistributionPointUrl))
        {
            var cdp = CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(
                new[] { _crlDistributionPointUrl }, critical: false);
            certGen.AddExtension(
                new DerObjectIdentifier(cdp.Oid!.Value!),
                cdp.Critical,
                Asn1Object.FromByteArray(cdp.RawData));
        }
    }

    private static X509RevocationReason ToX509Reason(RevocationReason reason) => reason switch
    {
        RevocationReason.KeyCompromise => X509RevocationReason.KeyCompromise,
        RevocationReason.CaCompromise => X509RevocationReason.CACompromise,
        RevocationReason.AffiliationChanged => X509RevocationReason.AffiliationChanged,
        RevocationReason.Superseded => X509RevocationReason.Superseded,
        RevocationReason.CessationOfOperation => X509RevocationReason.CessationOfOperation,
        RevocationReason.CertificateHold => X509RevocationReason.CertificateHold,
        RevocationReason.PrivilegeWithdrawn => X509RevocationReason.PrivilegeWithdrawn,
        RevocationReason.AaCompromise => X509RevocationReason.AACompromise,
        _ => X509RevocationReason.Unspecified
    };

    private static byte[] ComputeSubjectKeyIdentifier(SubjectPublicKeyInfo publicKeyInfo)
    {
        // SHA-1 hash of the public key as per RFC 5280 section 4.2.1.2
        var publicKeyData = publicKeyInfo.PublicKey.GetBytes();
        return System.Security.Cryptography.SHA1.HashData(publicKeyData);
    }

    private void CopySansFromCsr(X509V3CertificateGenerator certGen, CertificationRequestInfo csrInfo)
    {
        try
        {
            var attributes = csrInfo.Attributes;
            if (attributes == null)
                return;

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

                if (sanExtension != null)
                {
                    certGen.AddExtension(
                        X509Extensions.SubjectAlternativeName,
                        sanExtension.IsCritical,
                        sanExtension.GetParsedValue());

                    _logger.LogDebug("Copied SAN extension from CSR");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to copy SANs from CSR");
        }
    }

    private static int GetKeyUsage(List<string> allowedKeyUsages)
    {
        if (allowedKeyUsages == null || allowedKeyUsages.Count == 0)
        {
            // Default for end-entity certificates
            return KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment;
        }

        int usage = 0;
        foreach (var ku in allowedKeyUsages)
        {
            usage |= ku.ToLowerInvariant() switch
            {
                "digitalsignature" => KeyUsage.DigitalSignature,
                "nonrepudiation" or "contentcommitment" => KeyUsage.NonRepudiation,
                "keyencipherment" => KeyUsage.KeyEncipherment,
                "dataencipherment" => KeyUsage.DataEncipherment,
                "keyagreement" => KeyUsage.KeyAgreement,
                "keycertsign" => KeyUsage.KeyCertSign,
                "crlsign" => KeyUsage.CrlSign,
                "encipheronly" => KeyUsage.EncipherOnly,
                "decipheronly" => KeyUsage.DecipherOnly,
                _ => 0
            };
        }

        return usage;
    }

    private static List<DerObjectIdentifier> GetExtendedKeyUsages(List<string> allowedKeyUsages)
    {
        var ekus = new List<DerObjectIdentifier>();

        if (allowedKeyUsages == null)
            return ekus;

        foreach (var ku in allowedKeyUsages)
        {
            var oid = ku.ToLowerInvariant() switch
            {
                "serverauth" or "tlswebserverauthentication" => KeyPurposeID.id_kp_serverAuth,
                "clientauth" or "tlswebclientauthentication" => KeyPurposeID.id_kp_clientAuth,
                "codesigning" => KeyPurposeID.id_kp_codeSigning,
                "emailprotection" => KeyPurposeID.id_kp_emailProtection,
                "timestamping" => KeyPurposeID.id_kp_timeStamping,
                "ocspsigning" => KeyPurposeID.id_kp_OCSPSigning,
                _ => null
            };

            if (oid != null)
                ekus.Add(oid);
        }

        return ekus;
    }

    private static BigInteger GenerateSerialNumber()
    {
        // Generate a 128-bit random serial number
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        // Ensure positive by setting MSB to 0
        bytes[0] &= 0x7F;

        return new BigInteger(1, bytes);
    }

    private static string DetermineSignatureAlgorithm(AsymmetricKeyParameter privateKey)
    {
        return privateKey switch
        {
            RsaKeyParameters => "SHA256WithRSA",
            ECPrivateKeyParameters ec => ec.Parameters.N.BitLength switch
            {
                256 => "SHA256withECDSA",
                384 => "SHA384withECDSA",
                521 or 512 => "SHA512withECDSA",
                _ => "SHA256withECDSA"
            },
            _ => throw new NotSupportedException($"Unsupported key type: {privateKey.GetType().Name}")
        };
    }

    private static AsymmetricKeyParameter ConvertToBouncyCastlePrivateKey(X509Certificate2 cert)
    {
        if (cert.GetRSAPrivateKey() is RSA rsa)
        {
            // Try to export parameters (works on Linux/macOS)
            try
            {
                var rsaParams = rsa.ExportParameters(true);
                return new RsaPrivateCrtKeyParameters(
                    new BigInteger(1, rsaParams.Modulus!),
                    new BigInteger(1, rsaParams.Exponent!),
                    new BigInteger(1, rsaParams.D!),
                    new BigInteger(1, rsaParams.P!),
                    new BigInteger(1, rsaParams.Q!),
                    new BigInteger(1, rsaParams.DP!),
                    new BigInteger(1, rsaParams.DQ!),
                    new BigInteger(1, rsaParams.InverseQ!));
            }
            catch (CryptographicException)
            {
                // Windows CNG doesn't support exporting private key parameters
                // Use PKCS#8 export/import as a workaround
                var pkcs8 = rsa.ExportPkcs8PrivateKey();
                return Org.BouncyCastle.Security.PrivateKeyFactory.CreateKey(pkcs8);
            }
        }

        if (cert.GetECDsaPrivateKey() is ECDsa ecdsa)
        {
            // Try to export parameters (works on Linux/macOS)
            try
            {
                var ecParams = ecdsa.ExportParameters(true);
                var curve = GetBouncyCastleCurve(ecParams.Curve);
                var domainParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

                return new ECPrivateKeyParameters(
                    new BigInteger(1, ecParams.D!),
                    domainParams);
            }
            catch (CryptographicException)
            {
                // Windows CNG doesn't support exporting private key parameters
                // Use PKCS#8 export/import as a workaround
                var pkcs8 = ecdsa.ExportPkcs8PrivateKey();
                return Org.BouncyCastle.Security.PrivateKeyFactory.CreateKey(pkcs8);
            }
        }

        throw new NotSupportedException("Unsupported private key type");
    }

    private static Org.BouncyCastle.Asn1.X9.X9ECParameters GetBouncyCastleCurve(ECCurve curve)
    {
        // Map .NET named curves to BouncyCastle
        if (curve.Oid?.Value == ECCurve.NamedCurves.nistP256.Oid?.Value)
            return Org.BouncyCastle.Asn1.Nist.NistNamedCurves.GetByName("P-256");
        if (curve.Oid?.Value == ECCurve.NamedCurves.nistP384.Oid?.Value)
            return Org.BouncyCastle.Asn1.Nist.NistNamedCurves.GetByName("P-384");
        if (curve.Oid?.Value == ECCurve.NamedCurves.nistP521.Oid?.Value)
            return Org.BouncyCastle.Asn1.Nist.NistNamedCurves.GetByName("P-521");

        throw new NotSupportedException($"Unsupported EC curve: {curve.Oid?.Value}");
    }
}
