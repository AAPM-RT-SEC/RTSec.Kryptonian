using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using BcAttribute = Org.BouncyCastle.Asn1.Cms.Attribute;
using BcAttributeTable = Org.BouncyCastle.Asn1.Cms.AttributeTable;
using BcX509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace RTSec.Kryptonian.Infrastructure.Harness;

internal sealed class ScepClient : IDisposable
{
    private static readonly SecureRandom Rng = new();
    private static readonly DerObjectIdentifier OidMessageType = new("2.16.840.1.113733.1.9.2");
    private static readonly DerObjectIdentifier OidPkiStatus = new("2.16.840.1.113733.1.9.3");
    private static readonly DerObjectIdentifier OidSenderNonce = new("2.16.840.1.113733.1.9.5");
    private static readonly DerObjectIdentifier OidTransId = new("2.16.840.1.113733.1.9.7");

    private AsymmetricCipherKeyPair? _keyPair;
    private BcX509Certificate? _cert;

    // Build a SCEP PKCSReq (messageType=19) wrapping the given CSR DER.
    public byte[] BuildPkcsReq(byte[] csrDer, BcX509Certificate caCert)
    {
        var kpg = new RsaKeyPairGenerator();
        kpg.Init(new KeyGenerationParameters(Rng, 4096));
        _keyPair = kpg.GenerateKeyPair();
        _cert = SelfSignedCert(_keyPair);

        var edGen = new CmsEnvelopedDataGenerator();
        edGen.AddKeyTransRecipient(caCert);
        var enveloped = edGen.Generate(
            new CmsProcessableByteArray(csrDer),
            CmsEnvelopedDataGenerator.DesEde3Cbc);

        var txId = Convert.ToHexString(RandomBytes(16)).ToLowerInvariant();
        var nonce = RandomBytes(16);

        var attrs = new BcAttributeTable(new Dictionary<DerObjectIdentifier, object>
        {
            [CmsAttributes.ContentType] = new BcAttribute(CmsAttributes.ContentType,
                new DerSet(CmsObjectIdentifiers.EnvelopedData)),
            [OidMessageType] = new BcAttribute(OidMessageType,
                new DerSet(new DerPrintableString("19"))),
            [OidTransId] = new BcAttribute(OidTransId,
                new DerSet(new DerPrintableString(txId))),
            [OidSenderNonce] = new BcAttribute(OidSenderNonce,
                new DerSet(new DerOctetString(nonce)))
        });

        var sdGen = new CmsSignedDataGenerator();
        sdGen.AddCertificate(_cert);
        sdGen.AddSigner(_keyPair.Private, _cert, CmsSignedDataGenerator.DigestSha256, attrs, null);

        return sdGen.Generate(new CmsProcessableByteArray(enveloped.GetEncoded()), true).GetEncoded();
    }

    // Parse a SCEP CertRep (messageType=3) and return the issued certificate DER.
    public (byte[]? CertDer, string? Error) ParseCertRep(byte[] responseBytes)
    {
        try
        {
            var outer = new CmsSignedData(responseBytes);

            var signers = outer.GetSignerInfos().GetSigners();
            if (signers.Count > 0)
            {
                var statusAttr = signers[0].SignedAttributes?[OidPkiStatus];
                if (statusAttr is not null)
                {
                    var status = statusAttr.AttrValues[0].ToString();
                    if (status != "0")
                        return (null, $"SCEP pkiStatus={status} (failure)");
                }
            }

            byte[] inner;
            using (var ms = new MemoryStream())
            {
                outer.SignedContent.Write(ms);
                inner = ms.ToArray();
            }

            if (_keyPair is not null)
            {
                try
                {
                    var envData = new CmsEnvelopedData(inner);
                    foreach (RecipientInformation ri in envData.GetRecipientInfos().GetRecipients())
                    {
                        try { return (ri.GetContent(_keyPair.Private), null); }
                        catch { }
                    }
                }
                catch { }
            }

            // Fallback: server sent raw DER when no requester cert was found
            return (inner, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static BcX509Certificate SelfSignedCert(AsymmetricCipherKeyPair kp)
    {
        var gen = new X509V3CertificateGenerator();
        var dn = new X509Name("CN=SCEP-Client");
        gen.SetSubjectDN(dn);
        gen.SetIssuerDN(dn);
        gen.SetSerialNumber(BigInteger.ProbablePrime(64, Rng));
        gen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        gen.SetNotAfter(DateTime.UtcNow.AddHours(1));
        gen.SetPublicKey(kp.Public);
        return gen.Generate(new Asn1SignatureFactory("SHA256withRSA", kp.Private, Rng));
    }

    private static byte[] RandomBytes(int count)
    {
        var b = new byte[count];
        Rng.NextBytes(b);
        return b;
    }

    public void Dispose() { }
}
