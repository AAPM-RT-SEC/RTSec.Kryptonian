using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using System.Security.Cryptography;

namespace RTSec.Kryptonian.CaHarness.Backends;

public sealed class ScepAdcsEndpoint
{
    // SCEP attribute OIDs (Cisco/VeriSign arc)
    private static readonly DerObjectIdentifier OidMessageType    = new("2.16.840.1.113733.1.9.2");
    private static readonly DerObjectIdentifier OidPkiStatus      = new("2.16.840.1.113733.1.9.3");
    private static readonly DerObjectIdentifier OidSenderNonce    = new("2.16.840.1.113733.1.9.5");
    private static readonly DerObjectIdentifier OidRecipientNonce = new("2.16.840.1.113733.1.9.6");
    private static readonly DerObjectIdentifier OidTransId        = new("2.16.840.1.113733.1.9.7");

    private const string MessageTypePkcsReq = "19";
    private const string MessageTypeCertRep = "3";
    private const string PkiStatusSuccess   = "0";
    private const string PkiStatusFailure   = "2";

    private readonly AdcsHarnessBackend _adcs;

    public ScepAdcsEndpoint(AdcsHarnessBackend adcs) => _adcs = adcs;

    // Returns CA cert as degenerate PKCS#7 (content-type: application/x-x509-ca-cert)
    public byte[] GetCaCert()
    {
        var caCert = _adcs.GetCaCert();
        var gen = new CmsSignedDataGenerator();
        gen.AddCertificate(caCert);
        var signed = gen.Generate(new CmsProcessableByteArray(Array.Empty<byte>()), false);
        return signed.GetEncoded();
    }

    // Returns newline-delimited capability list (content-type: text/plain)
    public string GetCaCaps() => "SHA-256\nAES\nPOSTPKIOperation\n";

    // Full SCEP PKIOperation — returns DER PKCS#7 response
    public (byte[]? Response, string? Error) HandlePkiOperation(byte[] requestBody, string deviceId = "")
    {
        try
        {
            // 1. Parse outer SignedData
            var outerSd = new CmsSignedData(requestBody);
            var signerInfos = outerSd.GetSignerInfos().GetSigners();
            if (signerInfos.Count == 0)
                return (null, "No signer info in request");

            var signerInfo = signerInfos[0];
            var signedAttrs = signerInfo.SignedAttributes;

            // Extract SCEP attributes
            var transIdRaw     = signedAttrs[OidTransId]?.AttrValues[0];
            var senderNonceRaw = signedAttrs[OidSenderNonce]?.AttrValues[0];
            var msgTypeRaw     = signedAttrs[OidMessageType]?.AttrValues[0];

            var transId = transIdRaw?.ToString() ?? Guid.NewGuid().ToString("N");
            var senderNonce = senderNonceRaw != null
                ? Asn1OctetString.GetInstance(senderNonceRaw).GetOctets()
                : RandomNumberGenerator.GetBytes(16);
            var msgType = msgTypeRaw?.ToString() ?? MessageTypePkcsReq;

            if (msgType != MessageTypePkcsReq)
                return (null, $"Unsupported messageType: {msgType}");

            // 2. Decrypt the inner EnvelopedData (content of outer SignedData)
            byte[] innerBytes;
            using (var ms = new MemoryStream())
            {
                outerSd.SignedContent.Write(ms);
                innerBytes = ms.ToArray();
            }

            var envelopedData = new CmsEnvelopedData(innerBytes);
            var caKeyPair = _adcs.GetCaKeyPair();

            byte[]? csrDer = null;
            foreach (RecipientInformation ri in envelopedData.GetRecipientInfos().GetRecipients())
            {
                try
                {
                    csrDer = ri.GetContent(caKeyPair.Private);
                    break;
                }
                catch { /* try next recipient */ }
            }

            if (csrDer is null)
                return (null, "Could not decrypt EnvelopedData — ensure request is encrypted for this CA's public key");

            // 3. Sign the CSR
            var (record, error) = _adcs.IssueViaScep(csrDer, deviceId);
            if (record is null)
                return (BuildFailureResponse(transId, senderNonce, caKeyPair, _adcs.GetCaCert(), error ?? "Signing failed"), null);

            // 4. Build CertRep response
            var issuedCertDer = Convert.FromBase64String(record.CertificateDerBase64);
            var requesterCert = ExtractSignerCert(outerSd);

            return (BuildSuccessResponse(transId, senderNonce, caKeyPair, _adcs.GetCaCert(), issuedCertDer, requesterCert), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static X509Certificate? ExtractSignerCert(CmsSignedData sd)
    {
        try
        {
            var certStore = sd.GetCertificates();
            foreach (var si in sd.GetSignerInfos().GetSigners())
            {
                foreach (X509Certificate c in certStore.EnumerateMatches(si.SignerID))
                    return c;
            }
        }
        catch { }
        return null;
    }

    private static byte[] BuildSuccessResponse(
        string transId, byte[] recipientNonce,
        AsymmetricCipherKeyPair caKeyPair, X509Certificate caCert,
        byte[] issuedCertDer, X509Certificate? requesterCert)
    {
        // Wrap issued cert in EnvelopedData encrypted for requester
        byte[] responseContent;
        if (requesterCert is not null)
        {
            var edGen = new CmsEnvelopedDataGenerator();
            edGen.AddKeyTransRecipient(requesterCert);
            var envData = edGen.Generate(
                new CmsProcessableByteArray(issuedCertDer),
                CmsEnvelopedDataGenerator.DesEde3Cbc);
            responseContent = envData.GetEncoded();
        }
        else
        {
            // Fallback: no encryption (not RFC-compliant but usable for testing)
            responseContent = issuedCertDer;
        }

        return BuildSignedResponse(transId, recipientNonce, MessageTypeCertRep, PkiStatusSuccess,
            caKeyPair, caCert, responseContent);
    }

    private static byte[] BuildFailureResponse(
        string transId, byte[] recipientNonce,
        AsymmetricCipherKeyPair caKeyPair, X509Certificate caCert, string reason)
    {
        return BuildSignedResponse(transId, recipientNonce, MessageTypeCertRep, PkiStatusFailure,
            caKeyPair, caCert, System.Text.Encoding.UTF8.GetBytes(reason));
    }

    private static byte[] BuildSignedResponse(
        string transId, byte[] recipientNonce,
        string messageType, string pkiStatus,
        AsymmetricCipherKeyPair caKeyPair, X509Certificate caCert,
        byte[] content)
    {
        var newNonce = RandomNumberGenerator.GetBytes(16);

        var signedAttrsDict = new Dictionary<DerObjectIdentifier, object>
        {
            [OidMessageType]    = new Org.BouncyCastle.Asn1.Cms.Attribute(OidMessageType,    new DerSet(new DerPrintableString(messageType))),
            [OidPkiStatus]      = new Org.BouncyCastle.Asn1.Cms.Attribute(OidPkiStatus,      new DerSet(new DerPrintableString(pkiStatus))),
            [OidTransId]        = new Org.BouncyCastle.Asn1.Cms.Attribute(OidTransId,         new DerSet(new DerPrintableString(transId))),
            [OidRecipientNonce] = new Org.BouncyCastle.Asn1.Cms.Attribute(OidRecipientNonce, new DerSet(new DerOctetString(recipientNonce))),
            [OidSenderNonce]    = new Org.BouncyCastle.Asn1.Cms.Attribute(OidSenderNonce,    new DerSet(new DerOctetString(newNonce))),
        };

        var sdGen = new CmsSignedDataGenerator();
        sdGen.AddCertificate(caCert);
        sdGen.AddSigner(caKeyPair.Private, caCert,
            CmsSignedDataGenerator.DigestSha256,
            new AttributeTable(signedAttrsDict),
            null);

        var signed = sdGen.Generate(new CmsProcessableByteArray(content), true);
        return signed.GetEncoded();
    }
}
