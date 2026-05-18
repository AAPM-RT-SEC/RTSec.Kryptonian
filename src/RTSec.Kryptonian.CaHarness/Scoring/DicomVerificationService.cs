using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RTSec.Kryptonian.CaHarness.Backends;

namespace RTSec.Kryptonian.CaHarness.Scoring;

public sealed class DicomVerificationService
{
    public sealed record VerificationResult(string SerialNumber, string Thumbprint, string DeviceId, bool UsedGateway);

    // certHeaderValue is base64-encoded DER (no newlines — safe for HTTP headers)
    public VerificationResult? Verify(TeamEntry team, string backend, string certHeaderValue)
    {
        X509Certificate2 cert;
        try
        {
            var der = Convert.FromBase64String(certHeaderValue.Trim());
            cert = new X509Certificate2(der);
        }
        catch
        {
            return null;
        }

        var b = team.Backends.ResolveBackend(backend);
        if (b is null) return null;

        var caPem = b.GetCaCertificatePem();
        X509Certificate2 caCert;
        try
        {
            caCert = X509Certificate2.CreateFromPem(caPem);
        }
        catch
        {
            return null;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.CustomTrustStore.Add(caCert);
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;

        if (!chain.Build(cert))
            return null;

        var serial = cert.SerialNumber ?? "";
        // Recompute SHA-256 thumbprint to match what InMemoryCaEngine stores
        var sha256Thumbprint = Convert.ToHexString(SHA256.HashData(cert.RawData)).ToLowerInvariant();

        // Find the issued record to retrieve DeviceId
        var issued = b.GetIssued();
        var record = issued.FirstOrDefault(r =>
            r.SerialNumber.Equals(serial, StringComparison.OrdinalIgnoreCase) ||
            r.Thumbprint.Equals(sha256Thumbprint, StringComparison.OrdinalIgnoreCase));
        var deviceId = record?.DeviceId ?? "";

        // Gateway verification: check for protocol OID extensions embedded during gateway enrollment.
        // Certs issued via the JSON /issue API never carry these extensions, so they cannot be forged.
        var usedGateway = cert.Extensions[EstExtension.Oid] is not null
            || cert.Extensions[ScepExtension.Oid] is not null
            || cert.Extensions[EjbcaRestExtension.Oid] is not null;

        return new VerificationResult(serial, sha256Thumbprint, deviceId, usedGateway);
    }
}
