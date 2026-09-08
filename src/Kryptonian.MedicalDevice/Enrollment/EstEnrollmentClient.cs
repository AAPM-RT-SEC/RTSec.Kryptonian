using System.IO;
using System.Net;
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

/// <summary>
/// EST (RFC 7030) client for gateway enrollment and re-enrollment.
///
/// Pending enrolments: RFC 7030 section 3.5.2 answers an accepted-but-not-yet-issued
/// request with HTTP 202 plus Retry-After, and the client must repeat the SAME original
/// request to the SAME endpoint until the CA finishes. This client does exactly that: the
/// CSR bytes and the private key are produced once and reused on every attempt, and no
/// separate polling endpoint is used.
/// </summary>
public sealed class EstEnrollmentClient
{
    /// <summary>Default ceiling on pending retries so a stuck CA cannot spin forever.</summary>
    public const int DefaultMaxPendingAttempts = 10;

    /// <summary>Ceiling on one wait, so an absurd Retry-After cannot block the caller.</summary>
    internal static readonly TimeSpan MaxPendingDelay = TimeSpan.FromMinutes(15);

    /// <summary>Response media type required by EST for certificate responses.</summary>
    internal const string Pkcs7MimeMediaType = "application/pkcs7-mime";

    private readonly HttpClient _http;
    private readonly int _maxPendingAttempts;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public EstEnrollmentClient(
        HttpClient? http = null,
        int maxPendingAttempts = DefaultMaxPendingAttempts,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        if (maxPendingAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPendingAttempts), "Maximum pending attempts cannot be negative.");
        }

        _http = http ?? new HttpClient();
        _maxPendingAttempts = maxPendingAttempts;
        _delay = delay ?? Task.Delay;
    }

    public async Task<EstEnrollmentResult> EnrollAsync(
        Uri gateway,
        DeviceEnrollmentRequest device,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(device);

        // The key must stay alive across every pending retry, so this method is async and
        // awaits inside the using scope rather than returning the task un-awaited.
        using var rsa = RSA.Create(4096);
        var csrDer = BuildCsr(rsa, NormalizeCommonName(device.CommonName));

        return await SendWithPendingRetryAsync(
            BuildEstUri(gateway, "simpleenroll"),
            csrDer,
            rsa,
            request =>
            {
                request.Headers.Add("X-Activation-Code", device.ActivationCode);
                request.Headers.Add("X-Device-Manufacturer", device.Manufacturer);
                request.Headers.Add("X-Device-Model", device.Model);
                request.Headers.Add("X-Device-Serial-Number", device.SerialNumber);
            },
            "EST enrollment",
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Convenience overload: owns its handler and performs mTLS using
    /// <paramref name="existingClientCert"/>, which must carry a private key. Existing
    /// callers are unaffected.
    /// </summary>
    public static async Task<EstEnrollmentResult> ReenrollAsync(
        Uri gateway,
        X509Certificate2 existingClientCert,
        string commonName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(existingClientCert);
        ArgumentException.ThrowIfNullOrWhiteSpace(commonName);

        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual
        };
        handler.ClientCertificates.Add(existingClientCert);
        using var http = new HttpClient(handler);

        // Renewal rebuilds subject and SANs from the certificate itself, so the full
        // identity survives; commonName remains for caller compatibility.
        var client = new EstEnrollmentClient(http);
        return await client.ReenrollAsync(
            gateway,
            existingClientCert,
            existingClientCert.SubjectName,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Testable renewal entry point. The CALLER owns the handler, the HttpClient, and the
    /// client certificate presented over TLS; this instance only issues requests.
    /// </summary>
    public async Task<EstEnrollmentResult> ReenrollAsync(
        Uri gateway,
        X509Certificate2 existingClientCert,
        X500DistinguishedName existingSubject,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(existingClientCert);
        ArgumentNullException.ThrowIfNull(existingSubject);

        // Key rotation: the renewed certificate is bound to a brand-new private key. Awaited
        // inside the using scope so the temporary key is disposed on both success and failure
        // once CopyWithPrivateKey has taken its own copy.
        using var newKey = RSA.Create(4096);
        var csrDer = BuildRenewalCsr(newKey, existingClientCert, existingSubject);

        return await SendWithPendingRetryAsync(
            BuildEstUri(gateway, "simplereenroll"),
            csrDer,
            newKey,
            configureRequest: null,
            "EST re-enrollment",
            ct).ConfigureAwait(false);
    }

    // ── Pending (202) handling ────────────────────────────────────────────────

    private async Task<EstEnrollmentResult> SendWithPendingRetryAsync(
        Uri uri,
        byte[] csrDer,
        RSA privateKey,
        Action<HttpRequestMessage>? configureRequest,
        string operation,
        CancellationToken ct)
    {
        // privateKey is owned by the caller's using scope; this method never disposes it.
        // CopyWithPrivateKey inside DecodeCertificateResponse takes its own copy.
        var pendingWaits = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            using var request = BuildRequest(uri, csrDer, configureRequest);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                // Resolve the delay BEFORE counting the attempt: a missing or invalid
                // Retry-After must fail immediately rather than burn the retry budget.
                var delay = GetPendingDelay(response.Headers.RetryAfter, operation);

                if (pendingWaits >= _maxPendingAttempts)
                {
                    throw new InvalidOperationException(
                        $"{operation} still pending after {_maxPendingAttempts + 1} attempts; giving up.");
                }

                pendingWaits++;
                await _delay(delay, ct).ConfigureAwait(false);
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(
                    $"{operation} failed ({(int)response.StatusCode}): {body}");
            }

            EnsurePkcs7ContentType(response.Content.Headers.ContentType, operation);
            return DecodeCertificateResponse(body, privateKey);
        }
    }

    private static HttpRequestMessage BuildRequest(
        Uri uri,
        byte[] csrDer,
        Action<HttpRequestMessage>? configureRequest)
    {
        // Identical payload on every attempt, as RFC 7030 requires for a pending request.
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        configureRequest?.Invoke(request);
        request.Headers.Add("Content-Transfer-Encoding", "base64");
        request.Content = new StringContent(Convert.ToBase64String(csrDer), Encoding.ASCII);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");
        return request;
    }

    /// <summary>
    /// Resolves how long to wait before repeating a pending request. A missing or invalid
    /// Retry-After is fatal rather than polled aggressively: tight polling against a CA
    /// that never said when to return is worse than failing the enrollment.
    /// </summary>
    internal static TimeSpan GetPendingDelay(RetryConditionHeaderValue? retryAfter, string operation)
    {
        if (retryAfter is null)
        {
            throw new InvalidOperationException(
                $"{operation} returned 202 Accepted without a Retry-After header; refusing to poll.");
        }

        // A Retry-After that parsed into neither a delta nor a date is malformed; treat it
        // like a missing header rather than guessing a poll interval.
        TimeSpan delay;
        if (retryAfter.Delta.HasValue)
        {
            delay = retryAfter.Delta.Value;
        }
        else if (retryAfter.Date.HasValue)
        {
            delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
        }
        else
        {
            throw new InvalidOperationException(
                $"{operation} returned 202 Accepted with an invalid Retry-After header; refusing to poll.");
        }

        if (delay < TimeSpan.Zero)
        {
            // An HTTP-date already in the past means "come back now", but not in a hot loop.
            delay = TimeSpan.Zero;
        }

        if (delay > MaxPendingDelay)
        {
            throw new InvalidOperationException(
                $"{operation} requested a Retry-After of {delay}, above the {MaxPendingDelay} client ceiling.");
        }

        return delay;
    }

    private static void EnsurePkcs7ContentType(MediaTypeHeaderValue? contentType, string operation)
    {
        var mediaType = contentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mediaType) ||
            !string.Equals(mediaType, Pkcs7MimeMediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{operation} returned unexpected content type '{mediaType ?? "(none)"}'; " +
                $"expected '{Pkcs7MimeMediaType}'.");
        }
    }

    // ── CSR construction ──────────────────────────────────────────────────────

    private static byte[] BuildCsr(RSA rsa, string commonName)
    {
        var subject = new X500DistinguishedName($"CN={commonName}");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSigningRequest();
    }

    /// <summary>
    /// Builds the renewal CSR with a NEW key while preserving the current certificate's
    /// complete subject and its SubjectAlternativeName extension. Rebuilding from the CN
    /// alone would silently drop DNS/IP SANs that DICOM TLS peers validate against.
    /// </summary>
    internal static byte[] BuildRenewalCsr(
        RSA newKey,
        X509Certificate2 currentCert,
        X500DistinguishedName subject)
    {
        ArgumentNullException.ThrowIfNull(newKey);
        ArgumentNullException.ThrowIfNull(currentCert);
        ArgumentNullException.ThrowIfNull(subject);

        var request = new CertificateRequest(
            subject, newKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        CopySubjectAlternativeName(currentCert, request);

        return request.CreateSigningRequest();
    }

    private static void CopySubjectAlternativeName(
        X509Certificate2 currentCert,
        CertificateRequest request)
    {
        foreach (var extension in currentCert.Extensions)
        {
            if (extension.Oid?.Value != Oids.SubjectAltName || extension.RawData.Length == 0)
            {
                continue;
            }

            var san = new X509SubjectAlternativeNameExtension();
            san.CopyFrom(extension);
            request.CertificateExtensions.Add(san);
            return;
        }
    }

    private static class Oids
    {
        public const string SubjectAltName = "2.5.29.17";
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
        if (!gateway.IsAbsoluteUri || gateway.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("EST credentials require an absolute HTTPS endpoint.", nameof(gateway));

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

    internal static EstEnrollmentResult DecodeCertificateResponse(string body, RSA privateKey)
    {
        var pkcs7Der = Convert.FromBase64String(body.Trim());
        var signedCms = new SignedCms();
        signedCms.Decode(pkcs7Der);
        if (signedCms.Certificates.Count == 0)
        {
            throw new InvalidOperationException("Gateway response did not contain any certificates.");
        }

        var leaf = FindLeafCertificate(signedCms.Certificates, privateKey);
        var certificate = leaf.CopyWithPrivateKey(privateKey);
        var issuedCertificates = new X509Certificate2Collection();
        foreach (var cert in signedCms.Certificates)
        {
            issuedCertificates.Add(X509CertificateLoader.LoadCertificate(cert.RawData));
        }

        return new EstEnrollmentResult(certificate, issuedCertificates);
    }

    /// <summary>
    /// Picks the non-CA certificate whose public key matches the private key we generated.
    /// A gateway that returns some other leaf is rejected instead of producing a
    /// certificate/key pair that silently fails the first TLS handshake.
    /// </summary>
    /// <summary>
    /// Returns the non-CA certificate whose public key matches <paramref name="privateKey"/>.
    /// A gateway that hands back some other leaf is rejected here instead of yielding a
    /// certificate/key pair that fails on its first TLS handshake.
    /// </summary>
    internal static X509Certificate2 FindLeafCertificate(
        X509Certificate2Collection certs,
        RSA privateKey)
    {
        var expectedModulus = privateKey.ExportParameters(false).Modulus;

        foreach (var cert in certs)
        {
            var isCa = cert.Extensions.OfType<X509BasicConstraintsExtension>()
                .Any(basic => basic.CertificateAuthority);
            if (isCa)
            {
                continue;
            }

            using var publicKey = cert.GetRSAPublicKey();
            var modulus = publicKey?.ExportParameters(false).Modulus;
            if (modulus is not null && CryptographicOperations.FixedTimeEquals(modulus, expectedModulus))
            {
                return cert;
            }
        }

        throw new InvalidOperationException(
            "Gateway response contained no certificate matching the request key.");
    }
}
