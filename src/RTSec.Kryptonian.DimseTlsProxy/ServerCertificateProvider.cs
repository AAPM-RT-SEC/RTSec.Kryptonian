using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Asn1.X509;

namespace RTSec.Kryptonian.DimseTlsProxy;

// Generates (or loads from disk) the TLS server certificate presented to DICOM clients on port 4243.
// Teams download this cert once and add it to their client trust store.
public sealed class ServerCertificateProvider
{
    private const string CertPath = "/certs/proxy-server.pfx";
    private const string CertPassword = "kryptonian-proxy";

    public X509Certificate2 Certificate { get; }
    public string CertificatePem { get; }

    public ServerCertificateProvider(ILogger<ServerCertificateProvider> logger)
    {
        if (File.Exists(CertPath))
        {
            Certificate = new X509Certificate2(CertPath, CertPassword,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.PersistKeySet);
            logger.LogInformation("Loaded existing proxy server certificate from {Path}", CertPath);
        }
        else
        {
            Certificate = Generate();
            Directory.CreateDirectory(Path.GetDirectoryName(CertPath)!);
            File.WriteAllBytes(CertPath, Certificate.Export(X509ContentType.Pfx, CertPassword));
            logger.LogInformation("Generated new proxy server certificate, saved to {Path}", CertPath);
        }

        CertificatePem = ToPem(Certificate.RawData);
    }

    private static X509Certificate2 Generate()
    {
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(new RsaKeyGenerationParameters(
            BigInteger.ValueOf(65537), new SecureRandom(), 2048, 112));
        var keyPair = keyGen.GenerateKeyPair();

        var dn = new X509Name("CN=Kryptonian DIMSE Proxy,O=Kryptonian Hackathon,C=US");
        var serialBytes = new byte[16];
        RandomNumberGenerator.Fill(serialBytes);
        serialBytes[0] &= 0x7F;

        var certGen = new X509V3CertificateGenerator();
        certGen.SetSerialNumber(new BigInteger(1, serialBytes));
        certGen.SetSubjectDN(dn);
        certGen.SetIssuerDN(dn);
        certGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        certGen.SetNotAfter(DateTime.UtcNow.AddDays(30));
        certGen.SetPublicKey(keyPair.Public);
        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
        certGen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

        var bcCert = certGen.Generate(new Asn1SignatureFactory("SHA256WithRSA", keyPair.Private));
        var certDer = bcCert.GetEncoded();

        // Combine DER cert with private key into a .NET X509Certificate2 with private key
        var rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)keyPair.Private);
        using var rsa = RSA.Create();
        rsa.ImportParameters(rsaParams);
        var dotnetCert = new X509Certificate2(certDer);
        return dotnetCert.CopyWithPrivateKey(rsa);
    }

    private static string ToPem(byte[] der)
    {
        var b64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        return $"-----BEGIN CERTIFICATE-----\n{b64}\n-----END CERTIFICATE-----\n";
    }
}
