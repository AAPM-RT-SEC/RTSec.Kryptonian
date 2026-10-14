using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Kryptonian.MedicalDevice.Enrollment;

/// <summary>Explicit, application-local trust for the selected EST gateway.</summary>
public sealed record GatewayTrust(byte[] CertificateDer, bool SkipServerRevocation = false)
{
    public static GatewayTrust FromFile(string path)
    {
        var bytes = System.IO.File.ReadAllBytes(path);
        using var ca = X509CertificateLoader.LoadCertificate(bytes);
        if (!ca.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority)
            || ca.SubjectName.RawData.AsSpan().SequenceEqual(ca.IssuerName.RawData) == false)
            throw new ArgumentException("Choose the public root CA certificate for the EST server (.pem, .cer or .crt), not a server certificate or PFX.");
        return new GatewayTrust(ca.RawData);
    }

    public static HttpClient CreateClient(GatewayTrust? trust, X509Certificate2? clientCertificate = null)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true,
            ClientCertificateOptions = ClientCertificateOption.Manual };
        if (clientCertificate != null) handler.ClientCertificates.Add(clientCertificate);
        if (trust != null)
            handler.ServerCertificateCustomValidationCallback = (_, certificate, presentedChain, errors) =>
                trust.Validate(certificate, presentedChain, errors);
        return new HttpClient(handler);
    }

    internal bool Validate(X509Certificate2? certificate, X509Chain? presentedChain, SslPolicyErrors errors)
    {
        if (certificate == null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
            return false;
        using var root = X509CertificateLoader.LoadCertificate(CertificateDer);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        chain.ChainPolicy.RevocationMode = SkipServerRevocation ? X509RevocationMode.NoCheck : X509RevocationMode.Online;
        if (presentedChain != null)
            foreach (var element in presentedChain.ChainElements)
                chain.ChainPolicy.ExtraStore.Add(element.Certificate);
        return chain.Build(certificate);
    }
}
