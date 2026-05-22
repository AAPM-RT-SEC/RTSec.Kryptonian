using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Kryptonian.MedicalDevice.Enrollment;

public sealed record DeviceEnrollmentRequest(
    string CommonName,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string ActivationCode);

public sealed record EstEnrollmentResult(
    X509Certificate2 Certificate,
    X509Certificate2Collection IssuedCertificates);

public sealed class EstEnrollmentClient
{
    private readonly HttpClient _http;

    public EstEnrollmentClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
    }

    public async Task<EstEnrollmentResult> EnrollAsync(
        Uri gateway,
        DeviceEnrollmentRequest device,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(device);

        using var rsa = RSA.Create(4096);
        var csrDer = BuildCsr(rsa, NormalizeCommonName(device.CommonName));

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEstUri(gateway, "simpleenroll"));
        request.Headers.Add("X-Activation-Code", device.ActivationCode);
        request.Headers.Add("X-Device-Manufacturer", device.Manufacturer);
        request.Headers.Add("X-Device-Model", device.Model);
        request.Headers.Add("X-Device-Serial-Number", device.SerialNumber);
        request.Headers.Add("Content-Transfer-Encoding", "base64");
        request.Content = new StringContent(Convert.ToBase64String(csrDer), Encoding.ASCII);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"EST enrollment failed ({(int)response.StatusCode}): {body}");
        }

        return DecodeCertificateResponse(body, rsa);
    }

    public static async Task<EstEnrollmentResult> ReenrollAsync(
        Uri gateway,
        X509Certificate2 existingClientCert,
        string commonName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(existingClientCert);

        using var newRsa = RSA.Create(4096);
        var csrDer = BuildCsr(newRsa, NormalizeCommonName(commonName));

        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual
        };
        handler.ClientCertificates.Add(existingClientCert);
        using var http = new HttpClient(handler);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEstUri(gateway, "simplereenroll"));
        request.Headers.Add("Content-Transfer-Encoding", "base64");
        request.Content = new StringContent(Convert.ToBase64String(csrDer), Encoding.ASCII);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"EST re-enrollment failed ({(int)response.StatusCode}): {body}");
        }

        return DecodeCertificateResponse(body, newRsa);
    }

    public static X509Certificate2 LoadPfx(string path, string password)
    {
        var bytes = File.ReadAllBytes(path);
        try
        {
            return X509CertificateLoader.LoadPkcs12(bytes, string.Empty, X509KeyStorageFlags.Exportable);
        }
        catch (CryptographicException)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw;
            }

            return X509CertificateLoader.LoadPkcs12(bytes, password, X509KeyStorageFlags.Exportable);
        }
    }

    public static string? ExtractCommonName(X500DistinguishedName subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var part in subject.Format(true).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[3..].Trim();
            }
        }

        return null;
    }

    public static string NormalizeCommonName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();
        return trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
            ? trimmed[3..].Trim()
            : trimmed;
    }

    public static Uri BuildEstUri(Uri gateway, string operation)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var basePath = gateway.AbsolutePath;
        if (string.IsNullOrWhiteSpace(basePath) || basePath == "/")
        {
            basePath = "/.well-known/est";
        }

        basePath = basePath.TrimEnd('/');
        if (basePath.EndsWith("/simpleenroll", StringComparison.OrdinalIgnoreCase)
            || basePath.EndsWith("/simplereenroll", StringComparison.OrdinalIgnoreCase))
        {
            var lastSlash = basePath.LastIndexOf('/');
            basePath = lastSlash <= 0 ? "/.well-known/est" : basePath[..lastSlash];
        }

        var builder = new UriBuilder(gateway)
        {
            Path = $"{basePath}/{operation}",
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri;
    }

    private static byte[] BuildCsr(RSA rsa, string commonName)
    {
        var subject = new X500DistinguishedName($"CN={commonName}");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSigningRequest();
    }

    private static EstEnrollmentResult DecodeCertificateResponse(string body, RSA privateKey)
    {
        var pkcs7Der = Convert.FromBase64String(body.Trim());
        var signedCms = new SignedCms();
        signedCms.Decode(pkcs7Der);
        if (signedCms.Certificates.Count == 0)
        {
            throw new InvalidOperationException("Gateway response did not contain any certificates.");
        }

        var leaf = FindLeafCertificate(signedCms.Certificates);
        var certificate = leaf.CopyWithPrivateKey(privateKey);
        var issuedCertificates = new X509Certificate2Collection();
        foreach (var cert in signedCms.Certificates)
        {
            issuedCertificates.Add(X509CertificateLoader.LoadCertificate(cert.RawData));
        }

        return new EstEnrollmentResult(certificate, issuedCertificates);
    }

    private static X509Certificate2 FindLeafCertificate(X509Certificate2Collection certs)
    {
        foreach (var cert in certs)
        {
            foreach (var ext in cert.Extensions)
            {
                if (ext is X509BasicConstraintsExtension { CertificateAuthority: false })
                {
                    return cert;
                }
            }
        }

        return certs[0];
    }
}
