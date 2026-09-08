using System.Security.Cryptography.X509Certificates;

namespace Kryptonian.DICOMTls;

internal sealed record CertificateTrustResult(
    bool IsTrusted,
    string Subject,
    string Issuer,
    string? Reason);

internal static class CertificateTrustValidator
{
    public static CertificateTrustResult Validate(
        System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
        X509Certificate2Collection trustedCertificates, bool server = false)
    {
        if (certificate == null)
        {
            return new CertificateTrustResult(false, "(none)", "(none)", "No certificate was presented.");
        }

        if (certificate is X509Certificate2 peerCertificate)
        {
            return Validate(peerCertificate, trustedCertificates, server);
        }

        using var peer = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        return Validate(peer, trustedCertificates, server);
    }

    public static CertificateTrustResult Validate(
        X509Certificate2 peer,
        X509Certificate2Collection trustedCertificates, bool server = false)
    {
        var purpose = server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2";
        if (IsCertificateAuthority(peer) || !peer.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .Any(e => e.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>().Any(oid => oid.Value == purpose)))
            return new CertificateTrustResult(false, peer.Subject, peer.Issuer, "Peer is not an end-entity certificate with the required TLS purpose.");
        using var chain = new X509Chain();
        chain.ChainPolicy.ApplicationPolicy.Add(new System.Security.Cryptography.Oid(purpose));
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;

        foreach (var certificate in trustedCertificates)
        {
            if (string.Equals(certificate.Thumbprint, peer.Thumbprint, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsCertificateAuthority(certificate))
            {
                chain.ChainPolicy.CustomTrustStore.Add(certificate);
                chain.ChainPolicy.ExtraStore.Add(certificate);
            }
        }

        if (chain.ChainPolicy.CustomTrustStore.Count == 0)
        {
            return new CertificateTrustResult(false, peer.Subject, peer.Issuer, "No CA certificate was available from the EST response.");
        }

        var trusted = chain.Build(peer);
        if (trusted)
        {
            return new CertificateTrustResult(true, peer.Subject, peer.Issuer, null);
        }

        var reason = string.Join("; ", chain.ChainStatus.Select(status => status.StatusInformation.Trim()));
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "Certificate chain build failed.";
        }

        return new CertificateTrustResult(false, peer.Subject, peer.Issuer, reason);
    }

    private static bool IsCertificateAuthority(X509Certificate2 certificate)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (extension is X509BasicConstraintsExtension { CertificateAuthority: true })
            {
                return true;
            }
        }

        return false;
    }
}
