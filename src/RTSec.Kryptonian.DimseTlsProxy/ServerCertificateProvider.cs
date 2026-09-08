using System.Security.Cryptography.X509Certificates;

namespace RTSec.Kryptonian.DimseTlsProxy;

/// <summary>
/// Loads the proxy's TLS server certificate from configuration. The credential is issued and
/// rotated outside this process; nothing here generates, writes, caches, or falls back to a
/// stale copy. Every load re-reads the PFX so a replacement is picked up immediately.
/// </summary>
public sealed class ServerCertificateProvider
{
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    private readonly string _path;
    private readonly string _password;

    public ServerCertificateProvider(IConfiguration configuration, ILogger<ServerCertificateProvider> logger)
    {
        _path = configuration["Proxy:ServerCertificatePath"]
            ?? throw new InvalidOperationException("Proxy:ServerCertificatePath is required.");
        _password = configuration["Proxy:ServerCertificatePassword"]
            ?? throw new InvalidOperationException("Proxy:ServerCertificatePassword is required.");

        // Fail fast at startup rather than on first DICOM association.
        using var probe = LoadCertificate();
        logger.LogInformation("Proxy server certificate loaded for {Subject}", probe.Subject);
    }

    /// <summary>Returns a fresh, caller-owned instance; the caller disposes it.</summary>
    /// <summary>Public PEM only, read from the current on-disk credential.</summary>
    public string CertificatePem
    {
        get
        {
            using var cert = LoadCertificate();
            var b64 = Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks);
            return $"-----BEGIN CERTIFICATE-----\n{b64}\n-----END CERTIFICATE-----\n";
        }
    }

    /// <summary>Reads the PFX anew each call. Caller owns and must dispose the result.</summary>
    public X509Certificate2 LoadCertificate()
    {
        if (!File.Exists(_path))
        {
            throw new FileNotFoundException($"Proxy server certificate not found: {_path}");
        }

        var cert = new X509Certificate2(
            _path, _password, OperatingSystem.IsWindows() ? X509KeyStorageFlags.UserKeySet : X509KeyStorageFlags.EphemeralKeySet);

        try
        {
            Validate(cert);
            return cert;
        }
        catch
        {
            cert.Dispose();
            throw;
        }
    }

    private void Validate(X509Certificate2 cert)
    {
        if (!cert.HasPrivateKey)
        {
            throw new InvalidOperationException($"Proxy certificate '{_path}' has no private key.");
        }

        var now = DateTime.UtcNow;
        if (now < cert.NotBefore.ToUniversalTime())
        {
            throw new InvalidOperationException(
                $"Proxy certificate '{_path}' is not valid before {cert.NotBefore:O}.");
        }

        if (now > cert.NotAfter.ToUniversalTime())
        {
            throw new InvalidOperationException(
                $"Proxy certificate '{_path}' expired at {cert.NotAfter:O}; replace the file.");
        }

        var isCa = cert.Extensions.OfType<X509BasicConstraintsExtension>()
            .Any(basic => basic.CertificateAuthority);
        if (isCa)
        {
            throw new InvalidOperationException($"Proxy certificate '{_path}' is a CA certificate.");
        }

        var eku = cert.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
        var hasServerAuth = eku is not null && eku.EnhancedKeyUsages
            .Cast<System.Security.Cryptography.Oid>()
            .Any(o => o.Value == ServerAuthOid);
        if (!hasServerAuth)
        {
            throw new InvalidOperationException(
                $"Proxy certificate '{_path}' lacks an explicit serverAuth EKU.");
        }
    }
}
