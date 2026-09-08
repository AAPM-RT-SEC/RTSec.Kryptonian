using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>Operational certificate policy, not a claim of SDC clinical conformity.</summary>
public static class EnrollmentCertificatePolicy
{
    private const string SanOid = "2.5.29.17";

    public static CertificateRequest ValidateRequest(ParsedCsr csr, string approvedName, EstProfile profile,
        X509Certificate2? renewing = null)
    {
        var request = CertificateRequest.LoadSigningRequest(csr.RawData.ToArray(), HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions, RSASignaturePadding.Pkcs1);
        var signedRequest = new AsnReader(csr.RawData.ToArray(), AsnEncodingRules.DER).ReadSequence();
        signedRequest.ReadEncodedValue();
        var signatureOid = signedRequest.ReadSequence().ReadObjectIdentifier();
        if (signatureOid is not ("1.2.840.113549.1.1.11" or "1.2.840.113549.1.1.12" or "1.2.840.113549.1.1.13"
            or "1.2.840.113549.1.1.10" or "1.2.840.10045.4.3.2" or "1.2.840.10045.4.3.3" or "1.2.840.10045.4.3.4"))
            throw new CryptographicException("CSR must use a SHA-2 signature.");
        using var rsa = request.PublicKey.GetRSAPublicKey();
        using var ec = request.PublicKey.GetECDsaPublicKey();
        if (!(rsa?.KeySize >= 2048 || ec?.KeySize is 256 or 384 or 521))
            throw new CryptographicException("Device keys must be RSA >= 2048 or an approved NIST elliptic curve.");
        if (ec != null && ec.ExportParameters(false).Curve.Oid.Value is not
            ("1.2.840.10045.3.1.7" or "1.3.132.0.34" or "1.3.132.0.35"))
            throw new CryptographicException("Unsupported device elliptic curve.");

        _ = ExpectedEkus(profile);
        if (profile.ValidityDays is < 1 or > 3650)
            throw new CryptographicException("Operational certificate validity must be 1 to 3650 days.");
        var constraints = request.CertificateExtensions.SingleOrDefault(e => e.Oid?.Value == "2.5.29.19");
        if (constraints != null && new X509BasicConstraintsExtension(constraints, constraints.Critical).CertificateAuthority)
            throw new CryptographicException("Device enrollment cannot request a CA certificate.");

        var san = ExtensionBytes(request.CertificateExtensions, SanOid);
        if (renewing != null)
        {
            if (!request.SubjectName.RawData.AsSpan().SequenceEqual(renewing.SubjectName.RawData)
                || !san.AsSpan().SequenceEqual(ExtensionBytes(renewing.Extensions, SanOid)))
                throw new CryptographicException("Renewal must preserve the complete subject and SAN; identity changes require new approval.");
        }
        else
        {
            var subject = new AsnReader(request.SubjectName.RawData, AsnEncodingRules.DER).ReadSequence();
            var commonNames = 0;
            while (subject.HasData)
            {
                var attributes = subject.ReadSetOf();
                while (attributes.HasData)
                {
                    var attribute = attributes.ReadSequence();
                    if (attribute.ReadObjectIdentifier() != "2.5.4.3")
                        throw new CryptographicException("Initial operational subject must contain only the approved CN.");
                    var value = attribute.ReadCharacterString((UniversalTagNumber)attribute.PeekTag().TagValue);
                    if (!string.Equals(value, approvedName, StringComparison.OrdinalIgnoreCase))
                        throw new CryptographicException("CSR subject is not the approved inventory name.");
                    attribute.ThrowIfNotEmpty();
                    commonNames++;
                }
            }
            if (commonNames != 1) throw new CryptographicException("Exactly one approved common name is required.");
            if (san.Length == 0) return request;
            // Initial prototype inventory approves one name. URI/otherName and additional
            // names need an explicit inventory policy before enabling an SDC profile.
            var reader = new AsnReader(san, AsnEncodingRules.DER);
            var names = reader.ReadSequence();
            reader.ThrowIfNotEmpty();
            while (names.HasData)
            {
                var tag = names.PeekTag();
                var name = tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 2
                    ? names.ReadCharacterString(UniversalTagNumber.IA5String, new Asn1Tag(TagClass.ContextSpecific, 2))
                    : tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 7
                        ? new IPAddress(names.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 7))).ToString()
                        : throw new CryptographicException("SAN type is not authorized by this inventory policy.");
                if (!string.Equals(name, approvedName, StringComparison.OrdinalIgnoreCase))
                    throw new CryptographicException("Requested SAN is not the approved inventory name.");
            }
        }
        return request;
    }

    public static void ValidateIssued(CertificateRequest request, EstProfile profile,
        X509Certificate2 leaf, IEnumerable<X509Certificate2> responseChain, X509Certificate2[] authorizedCas)
    {
        if (!request.PublicKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(leaf.PublicKey.ExportSubjectPublicKeyInfo())
            || !request.SubjectName.RawData.AsSpan().SequenceEqual(leaf.SubjectName.RawData)
            || !ExtensionBytes(request.CertificateExtensions, SanOid).AsSpan().SequenceEqual(ExtensionBytes(leaf.Extensions, SanOid)))
            throw new CryptographicException("CA returned a certificate with a different key or identity.");
        if (leaf.Extensions.OfType<X509BasicConstraintsExtension>().SingleOrDefault() is not { CertificateAuthority: false })
            throw new CryptographicException("CA must return an end-entity certificate.");
        var usage = leaf.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault()?.KeyUsages;
        var expectedUsage = X509KeyUsageFlags.None;
        foreach (var allowed in profile.AllowedKeyUsages)
            expectedUsage |= allowed.ToLowerInvariant() switch
            {
                "digitalsignature" => X509KeyUsageFlags.DigitalSignature,
                "keyencipherment" => X509KeyUsageFlags.KeyEncipherment,
                "keyagreement" => X509KeyUsageFlags.KeyAgreement,
                _ => X509KeyUsageFlags.None
            };
        if (usage == null || usage != expectedUsage || (usage.Value & X509KeyUsageFlags.DigitalSignature) == 0)
            throw new CryptographicException("CA returned unsafe device key usages.");
        var ekus = leaf.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
        if (ekus == null || !ExpectedEkus(profile).SetEquals(ekus.EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value!)))
            throw new CryptographicException("CA returned EKUs different from the approved profile.");
        var now = DateTime.UtcNow;
        if (leaf.NotBefore.ToUniversalTime() > now || leaf.NotAfter.ToUniversalTime() <= now
            || leaf.NotAfter.ToUniversalTime() > now.AddDays(profile.ValidityDays).AddMinutes(5))
            throw new CryptographicException("CA returned a certificate outside the approved validity period.");

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // issuance check; peers check revocation separately
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.CustomTrustStore.AddRange(authorizedCas);
        chain.ChainPolicy.ExtraStore.AddRange(responseChain.ToArray());
        if (authorizedCas.Length == 0 || !chain.Build(leaf))
            throw new CryptographicException("CA response does not chain to this profile's authorized issuer.");
        if (chain.ChainElements.Cast<X509ChainElement>().Skip(1).Any(e => leaf.NotAfter > e.Certificate.NotAfter))
            throw new CryptographicException("Device certificate outlives its issuer.");
    }

    public static HashSet<string> ExpectedEkus(EstProfile profile)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var usage in profile.AllowedKeyUsages)
        {
            var oid = usage.ToLowerInvariant() switch
            {
                "digitalsignature" or "keyencipherment" or "keyagreement" => null,
                "clientauth" or "tlswebclientauthentication" => "1.3.6.1.5.5.7.3.2",
                "serverauth" or "tlswebserverauthentication" => "1.3.6.1.5.5.7.3.1",
                _ => throw new CryptographicException($"Unsupported operational certificate usage: {usage}")
            };
            if (oid != null) result.Add(oid);
        }
        if (result.Count == 0 || !profile.AllowedKeyUsages.Contains("digitalSignature", StringComparer.OrdinalIgnoreCase))
            throw new CryptographicException("Configure digitalSignature and explicit clientAuth and/or serverAuth in the operational profile.");
        return result;
    }

    private static byte[] ExtensionBytes(System.Collections.IEnumerable extensions, string oid) =>
        extensions.Cast<X509Extension>().SingleOrDefault(e => e.Oid?.Value == oid)?.RawData ?? Array.Empty<byte>();
}
