using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Kryptonian.DICOMTls;

internal static class GatewayHttpClient
{
    public static HttpClient Create(Uri gateway, string? caPath)
    {
        if (string.IsNullOrWhiteSpace(caPath)) return new HttpClient { BaseAddress = gateway };
        using var ca = X509Certificate2.CreateFromPem(File.ReadAllText(caPath));
        if (!ca.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority))
            throw new ArgumentException("Gateway trust file must contain a CA certificate.", nameof(caPath));
        var caDer = ca.RawData;
        Console.WriteLine("WARNING: explicit gateway CA validation checks identity and chain but not revocation of the developer HTTPS server certificate. DICOM peer policy is separate.");
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, cert, _, errors) => Validate(cert, errors, caDer)
        };
        return new HttpClient(handler) { BaseAddress = gateway };
    }

    internal static bool Validate(X509Certificate2? certificate, SslPolicyErrors errors, byte[] caDer)
    {
        if (certificate == null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
            return false;
        using var ca = X509CertificateLoader.LoadCertificate(caDer);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // explicitly scoped developer HTTPS exception
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        return certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .Any(e => e.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == "1.3.6.1.5.5.7.3.1")) && chain.Build(certificate);
    }
}
