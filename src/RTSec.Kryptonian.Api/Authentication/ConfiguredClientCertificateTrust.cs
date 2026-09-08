using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RTSec.Kryptonian.Api.Authentication;

public sealed class ConfiguredClientCertificateTrust
{
    private readonly X509Certificate2Collection _roots = new();

    public ConfiguredClientCertificateTrust(IEnumerable<string> caFiles)
    {
        foreach (var path in caFiles) _roots.ImportFromPemFile(path);
        if (_roots.Count == 0 || _roots.Cast<X509Certificate2>().Any(c =>
            !c.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority)))
            throw new InvalidOperationException("Client TLS trust requires CA certificates.");
    }

    public bool Validate(X509Certificate2 certificate, X509Chain? presentedChain,
        X509RevocationMode revocationMode = X509RevocationMode.Online)
    {
        if (certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority)
            || !certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
                .Any(e => e.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == "1.3.6.1.5.5.7.3.2"))) return false;
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(_roots);
        if (presentedChain != null)
            chain.ChainPolicy.ExtraStore.AddRange(presentedChain.ChainElements.Cast<X509ChainElement>().Select(e => e.Certificate).ToArray());
        chain.ChainPolicy.RevocationMode = revocationMode;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.2"));
        return chain.Build(certificate);
    }
}
