using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace RTSec.Kryptonian.CaHarness.Backends;

public sealed record IssuedCertRecord(
    string SerialNumber,
    string Thumbprint,
    string SubjectDn,
    string CertificatePem,
    string CertificateDerBase64,
    DateTime NotBeforeUtc,
    DateTime NotAfterUtc,
    bool IsRevoked,
    string DeviceId,
    bool IsEstEnrolled);

// OID embedded in certs issued via the EST simpleenroll path.
// Presence of this extension is the only enforced proof of EST enrollment.
public static class EstExtension
{
    public const string Oid = "1.3.6.1.4.1.99999.1";
}

public sealed class InMemoryCaEngine
{
    private sealed record CaState(AsymmetricCipherKeyPair KeyPair, X509Certificate Cert);

    private readonly string _displayName;
    private volatile CaState _ca;
    private readonly ConcurrentDictionary<string, IssuedCertRecord> _issued = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _revoked = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryCaEngine(string displayName)
    {
        _displayName = displayName;
        _ca = BuildCa();
    }

    public string IssuerName => $"CN={_displayName} Harness CA";

    public void Reset()
    {
        _issued.Clear();
        _revoked.Clear();
        _ca = BuildCa();
    }

    public string GetCaCertificatePem() => ToPem(_ca.Cert.GetEncoded(), "CERTIFICATE");

    public (IssuedCertRecord? Record, string? Error) Sign(byte[] csrDer, int validityDays, string deviceId = "", bool estEnrolled = false)
    {
        try
        {
            var ca = _ca;

            var pkcs10 = new Pkcs10CertificationRequest(csrDer);
            if (!pkcs10.Verify())
                return (null, "CSR signature verification failed");

            var csrInfo = pkcs10.GetCertificationRequestInfo();

            var serialBytes = new byte[16];
            RandomNumberGenerator.Fill(serialBytes);
            serialBytes[0] &= 0x7F;
            var serial = new BigInteger(1, serialBytes);
            var serialHex = serial.ToString(16).ToUpperInvariant();

            var notBefore = DateTime.UtcNow.AddMinutes(-5);
            var notAfter = DateTime.UtcNow.AddDays(validityDays);

            var certGen = new X509V3CertificateGenerator();
            certGen.SetSerialNumber(serial);
            certGen.SetSubjectDN(csrInfo.Subject);
            certGen.SetIssuerDN(ca.Cert.SubjectDN);
            certGen.SetNotBefore(notBefore);
            certGen.SetNotAfter(notAfter);
            certGen.SetPublicKey(pkcs10.GetPublicKey());

            certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
            certGen.AddExtension(X509Extensions.KeyUsage, true,
                new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

            var ski = SHA1.HashData(csrInfo.SubjectPublicKeyInfo.PublicKey.GetBytes());
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifier(ski));
            certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
                new AuthorityKeyIdentifierStructure(ca.Cert));

            if (estEnrolled)
            {
                // Marks this cert as EST-enrolled. Presence of this extension is checked
                // during DICOM scoring to prove the device enrolled through a gateway.
                var estOid = new Org.BouncyCastle.Asn1.DerObjectIdentifier(EstExtension.Oid);
                certGen.AddExtension(estOid, false,
                    new Org.BouncyCastle.Asn1.DerUtf8String("est-enrolled"));
            }

            var signatureFactory = new Asn1SignatureFactory("SHA256WithRSA", ca.KeyPair.Private);
            var cert = certGen.Generate(signatureFactory);
            var certDer = cert.GetEncoded();

            var thumbprint = Convert.ToHexString(SHA256.HashData(certDer)).ToLowerInvariant();

            var record = new IssuedCertRecord(
                SerialNumber: serialHex,
                Thumbprint: thumbprint,
                SubjectDn: csrInfo.Subject.ToString(),
                CertificatePem: ToPem(certDer, "CERTIFICATE"),
                CertificateDerBase64: Convert.ToBase64String(certDer),
                NotBeforeUtc: notBefore,
                NotAfterUtc: notAfter,
                IsRevoked: false,
                DeviceId: deviceId,
                IsEstEnrolled: estEnrolled);

            _issued[serialHex] = record;
            return (record, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public bool Revoke(string serial)
    {
        var key = serial.ToUpperInvariant();
        if (!_issued.TryGetValue(key, out var record))
            return false;

        _revoked[key] = true;
        _issued[key] = record with { IsRevoked = true };
        return true;
    }

    public IReadOnlyList<IssuedCertRecord> GetIssued() => _issued.Values.ToList();

    private CaState BuildCa()
    {
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(new RsaKeyGenerationParameters(
            BigInteger.ValueOf(65537),
            new SecureRandom(),
            2048, 112));
        var keyPair = keyGen.GenerateKeyPair();

        var dn = new X509Name($"CN={_displayName} Harness CA,O=Kryptonian Hackathon,C=US");

        var serialBytes = new byte[16];
        RandomNumberGenerator.Fill(serialBytes);
        serialBytes[0] &= 0x7F;
        var serial = new BigInteger(1, serialBytes);

        var certGen = new X509V3CertificateGenerator();
        certGen.SetSerialNumber(serial);
        certGen.SetSubjectDN(dn);
        certGen.SetIssuerDN(dn);
        certGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        certGen.SetNotAfter(DateTime.UtcNow.AddYears(10));
        certGen.SetPublicKey(keyPair.Public);

        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
        certGen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));

        var signatureFactory = new Asn1SignatureFactory("SHA256WithRSA", keyPair.Private);
        var cert = certGen.Generate(signatureFactory);
        return new CaState(keyPair, cert);
    }

    internal static string ToPem(byte[] der, string label)
    {
        var sb = new StringBuilder();
        sb.Append($"-----BEGIN {label}-----\n");
        sb.Append(Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks));
        sb.Append($"\n-----END {label}-----\n");
        return sb.ToString();
    }
}
