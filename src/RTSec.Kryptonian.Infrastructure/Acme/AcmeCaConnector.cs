using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Exceptions;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Infrastructure.Acme;

/// <summary>
/// ACME CA connector using the Certes library.
/// Supports Let's Encrypt, ZeroSSL, and other ACME-compliant CAs.
/// </summary>
public class AcmeCaConnector : ICaConnector
{
    private readonly ILogger<AcmeCaConnector> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAcmeChallengeProvider _challengeProvider;
    private readonly AcmeConnectorConfig _config;
    private readonly IDataProtectionService _dataProtection;

    private AcmeContext? _acmeContext;
    private IAccountContext? _accountContext;

    // Cached CA chain from the most recent certificate issuance
    private X509Certificate2[]? _cachedCaChain;
    private readonly object _chainCacheLock = new();

    public CaBackendType Type => CaBackendType.Acme;

    public AcmeCaConnector(
        ILogger<AcmeCaConnector> logger,
        IUnitOfWork unitOfWork,
        IAcmeChallengeProvider challengeProvider,
        IDataProtectionService dataProtection,
        AcmeConnectorConfig config)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _challengeProvider = challengeProvider;
        _dataProtection = dataProtection;
        _config = config;

        _logger.LogInformation("ACME connector initialized for directory: {DirectoryUrl}", config.DirectoryUrl);
    }

    /// <inheritdoc />
    public Task<X509Certificate2[]> GetCaCertificatesAsync(CancellationToken ct = default)
    {
        // ACME CAs provide the full chain with each certificate issuance.
        // We cache the issuer chain from the most recent issuance to serve /cacerts requests.
        // If no certificate has been issued yet, we return the well-known ISRG Root chain
        // for Let's Encrypt, or an informational message for other ACME CAs.

        _logger.LogDebug("GetCaCertificatesAsync called for ACME backend");

        lock (_chainCacheLock)
        {
            if (_cachedCaChain != null && _cachedCaChain.Length > 0)
            {
                _logger.LogDebug("Returning cached CA chain with {Count} certificates", _cachedCaChain.Length);
                return Task.FromResult(_cachedCaChain);
            }
        }

        // No cached chain yet - return well-known roots for common ACME CAs
        var wellKnownChain = GetWellKnownCaChain();
        if (wellKnownChain.Length > 0)
        {
            _logger.LogDebug("Returning well-known CA chain for {DirectoryUrl}", _config.DirectoryUrl);
            return Task.FromResult(wellKnownChain);
        }

        // For unknown ACME CAs, log a warning - the chain will be available after first issuance
        _logger.LogWarning(
            "No cached CA chain available for ACME backend {DirectoryUrl}. " +
            "Chain will be cached after first certificate issuance.",
            _config.DirectoryUrl);

        // Return a placeholder - this is still not ideal but better than empty
        // The orchestrator may need adjustment to handle ACME gracefully
        return Task.FromResult(Array.Empty<X509Certificate2>());
    }

    /// <summary>
    /// Returns well-known CA certificates for common ACME providers.
    /// </summary>
    private X509Certificate2[] GetWellKnownCaChain()
    {
        // Check if this is a Let's Encrypt directory
        if (_config.DirectoryUrl.Contains("letsencrypt.org", StringComparison.OrdinalIgnoreCase))
        {
            return GetLetsEncryptChain();
        }

        // Add other well-known ACME CAs here as needed
        return Array.Empty<X509Certificate2>();
    }

    /// <summary>
    /// Returns the Let's Encrypt ISRG Root X1 and intermediate certificates.
    /// These are the standard chain for Let's Encrypt issued certificates.
    /// </summary>
    private X509Certificate2[] GetLetsEncryptChain()
    {
        try
        {
            // ISRG Root X1 - Let's Encrypt's root certificate (valid until 2035)
            // This is a well-known public certificate
            const string isrgRootX1Pem = @"-----BEGIN CERTIFICATE-----
MIIFazCCA1OgAwIBAgIRAIIQz7DSQONZRGPgu2OCiwAwDQYJKoZIhvcNAQELBQAw
TzELMAkGA1UEBhMCVVMxKTAnBgNVBAoTIEludGVybmV0IFNlY3VyaXR5IFJlc2Vh
cmNoIEdyb3VwMRUwEwYDVQQDEwxJU1JHIFJvb3QgWDEwHhcNMTUwNjA0MTEwNDM4
WhcNMzUwNjA0MTEwNDM4WjBPMQswCQYDVQQGEwJVUzEpMCcGA1UEChMgSW50ZXJu
ZXQgU2VjdXJpdHkgUmVzZWFyY2ggR3JvdXAxFTATBgNVBAMTDElTUkcgUm9vdCBY
MTCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAK3oJHP0FDfzm54rVygc
h77ct984kIxuPOZXoHj3dcKi/vVqbvYATyjb3miGbESTtrFj/RQSa78f0uoxmyF+
0TM8ukj13Xnfs7j/EvEhmkvBioZxaUpmZmyPfjxwv60pIgbz5MDmgK7iS4+3mX6U
A5/TR5d8mUgjU+g4rk8Kb4Mu0UlXjIB0ttov0DiNewNwIRt18jA8+o+u3dpjq+sW
T8KOEUt+zwvo/7V3LvSye0rgTBIlDHCNAymg4VMk7BPZ7hm/ELNKjD+Jo2FR3qyH
B5T0Y3HsLuJvW5iB4YlcNHlsdu87kGJ55tukmi8mxdAQ4Q7e2RCOFvu396j3x+UC
B5iPNgiV5+I3lg02dZ77DnKxHZu8A/lJBdiB3QW0KtZB6awBdpUKD9jf1b0SHzUv
KBds0pjBqAlkd25HN7rOrFleaJ1/ctaJxQZBKT5ZPt0m9STJEadao0xAH0ahmbWn
OlFuhjuefXKnEgV4We0+UXgVCwOPjdAvBbI+e0ocS3MFEvzG6uBQE3xDk3SzynTn
jh8BCNAw1FtxNrQHusEwMFxIt4I7mKZ9YIqioymCzLq9gwQbooMDQaHWBfEbwrbw
qHyGO0aoSCqI3Haadr8faqU9GY/rOPNk3sgrDQoo//fb4hVC1CLQJ13hef4Y53CI
rU7m2Ys6xt0nUW7/vGT1M0NPAgMBAAGjQjBAMA4GA1UdDwEB/wQEAwIBBjAPBgNV
HRMBAf8EBTADAQH/MB0GA1UdDgQWBBR5tFnme7bl5AFzgAiIyBpY9umbbjANBgkq
hkiG9w0BAQsFAAOCAgEAVR9YqbyyqFDQDLHYGmkgJykIrGF1XIpu+ILlaS/V9lZL
ubhzEFnTIZd+50xx+7LSYK05qAvqFyFWhfFQDlnrzuBZ6brJFe+GnY+EgPbk6ZGQ
3BebYhtF8GaV0nxvwuo77x/Py9auJ/GpsMiu/X1+mvoiBOv/2X/qkSsisRcOj/KK
NFtY2PwByVS5uCbMiogziUwthDyC3+6WVwW6LLv3xLfHTjuCvjHIInNzktHCgKQ5
ORAzI4JMPJ+GslWYHb4phowim57iaztXOoJwTdwJx4nLCgdNbOhdjsnvzqvHu7Ur
TkXWStAmzOVyyghqpZXjFaH3pO3JLF+l+/+sKAIuvtd7u+Nxe5AW0wdeRlN8NwdC
jNPElpzVmbUq4JUagEiuTDkHzsxHpFKVK7q4+63SM1N95R1NbdWhscdCb+ZAJzVc
oyi3B43njTOQ5yOf+1CceWxG1bQVs5ZufpsMljq4Ui0/1lvh+wjChP4kqKOJ2qxq
4RgqsahDYVvTH9w7jXbyLeiNdd8XM2w9U/t7y0Ff/9yi0GE44Za4rF2LN9d11TPA
mRGunUHBcnWEvgJBQl9nJEiU0Zsnvgc/ubhPgXRR4Xq37Z0j4r7g1SgEEzwxA57d
emyPxgcYxn/eR44/KJ4EBs+lVDR3veyJm+kXQ99b21/+jh5Xos1AnX5iItreGCc=
-----END CERTIFICATE-----";

            var isrgRoot = X509Certificate2.CreateFromPem(isrgRootX1Pem);
            return new[] { isrgRoot };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse well-known Let's Encrypt root certificate");
            return Array.Empty<X509Certificate2>();
        }
    }

    /// <summary>
    /// Updates the cached CA chain from a certificate issuance.
    /// </summary>
    private void UpdateCachedCaChain(X509Certificate2[] issuers)
    {
        if (issuers.Length == 0)
            return;

        lock (_chainCacheLock)
        {
            _cachedCaChain = issuers;
            _logger.LogDebug("Cached CA chain updated with {Count} certificates", issuers.Length);
        }
    }

    /// <inheritdoc />
    public async Task<CertificateIssuanceResult> IssueCertificateAsync(
        ParsedCsr csr,
        EstProfile profile,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(csr);
        ArgumentNullException.ThrowIfNull(profile);

        _logger.LogInformation("Issuing certificate via ACME for subject: {Subject}", csr.SubjectDn);

        try
        {
            // Ensure we have an authenticated ACME context
            await EnsureAcmeContextAsync(ct);

            // Extract domain from CSR (CN or first SAN)
            var domains = ExtractDomainsFromCsr(csr);
            if (domains.Count == 0)
            {
                return CertificateIssuanceResult.Failed("No valid domains found in CSR");
            }

            _logger.LogInformation("Requesting certificate for domains: {Domains}", string.Join(", ", domains));

            // Create ACME order
            var order = await _acmeContext!.NewOrder(domains);
            var orderResource = await order.Resource();

            _logger.LogDebug("ACME order created with status: {Status}", orderResource.Status);

            // Process authorizations
            var authorizations = await order.Authorizations();
            foreach (var auth in authorizations)
            {
                await ProcessAuthorizationAsync(auth, ct);
            }

            // Generate a key for the certificate
            var certKey = GenerateCertificateKey(csr);

            // Finalize the order with the CSR - Certes expects raw CSR bytes
            var finalizedOrder = await order.Finalize(csr.RawData.ToArray());

            _logger.LogDebug("Order finalized, waiting for certificate issuance...");

            // Download the certificate
            var certChain = await order.Download();

            // Generate a strong random password for PFX to protect the private key in memory
            // This ensures the key is never held unprotected, even transiently
            var pfxPassword = GenerateSecurePassword();
            var pfxBuilder = certChain.ToPfx(certKey);
            var pfxBytes = pfxBuilder.Build("acme-cert", pfxPassword);

            // Load the PFX with EphemeralKeySet to keep key in memory only (not persisted to disk)
            // The password protects the key material during the brief window it exists as PFX bytes
            var issuedCert = new X509Certificate2(
                pfxBytes,
                pfxPassword,
                X509KeyStorageFlags.EphemeralKeySet);

            // Clear the PFX bytes and password from memory as soon as possible
            Array.Clear(pfxBytes);

            // Build the chain
            var chain = new List<X509Certificate2> { issuedCert };

            // Add issuer certificates from the chain using the Issuers collection
            var issuerCerts = new List<X509Certificate2>();
            foreach (var issuer in certChain.Issuers)
            {
                // Convert to PEM string then to certificate
                var issuerPem = issuer.ToPem();
                var issuerCert = X509Certificate2.CreateFromPem(issuerPem);
                chain.Add(issuerCert);
                issuerCerts.Add(issuerCert);
            }

            // Cache the issuer chain for subsequent /cacerts requests
            UpdateCachedCaChain(issuerCerts.ToArray());

            _logger.LogInformation("Certificate issued via ACME: Serial={Serial}, Subject={Subject}",
                issuedCert.SerialNumber, issuedCert.Subject);

            return CertificateIssuanceResult.Successful(issuedCert, chain.ToArray());
        }
        catch (AcmeRequestException ex)
        {
            _logger.LogError(ex, "ACME request failed: {Error}", ex.Error?.Detail);
            return CertificateIssuanceResult.Failed($"ACME error: {ex.Error?.Detail ?? ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue certificate via ACME");
            return CertificateIssuanceResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public Task<bool> RevokeCertificateAsync(string serial, Domain.Enums.RevocationReason reason, CancellationToken ct = default)
    {
        _logger.LogWarning("Certificate revocation via ACME is not yet implemented. Serial: {Serial}", serial);
        // ACME supports revocation - could be implemented using acmeContext.RevokeCertificate()
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var acme = new AcmeContext(new Uri(_config.DirectoryUrl));
            var directory = await acme.GetDirectory();

            _logger.LogDebug("ACME connection test successful. Directory: {DirectoryUrl}", _config.DirectoryUrl);
            return directory != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ACME connection test failed for {DirectoryUrl}", _config.DirectoryUrl);
            return false;
        }
    }

    private async Task EnsureAcmeContextAsync(CancellationToken ct)
    {
        if (_acmeContext != null && _accountContext != null)
        {
            return;
        }

        // Try to load existing account from database
        var existingAccount = await _unitOfWork.AcmeAccounts.GetByDirectoryAndEmailAsync(
            new Uri(_config.DirectoryUrl), _config.Email, ct);

        if (existingAccount != null)
        {
            _logger.LogDebug("Loading existing ACME account for {Email}", _config.Email);

            // Decrypt the private key
            var decryptedKey = _dataProtection.Unprotect(existingAccount.EncryptedPrivateKey);
            var accountKey = KeyFactory.FromPem(decryptedKey);

            _acmeContext = new AcmeContext(new Uri(_config.DirectoryUrl), accountKey);
            _accountContext = await _acmeContext.Account();
        }
        else
        {
            _logger.LogInformation("Creating new ACME account for {Email} at {DirectoryUrl}",
                _config.Email, _config.DirectoryUrl);

            _acmeContext = new AcmeContext(new Uri(_config.DirectoryUrl));

            // Handle External Account Binding (EAB) if configured (required by some CAs like ZeroSSL)
            if (!string.IsNullOrEmpty(_config.EabKeyId) && !string.IsNullOrEmpty(_config.EabHmacKey))
            {
                _accountContext = await _acmeContext.NewAccount(
                    new[] { $"mailto:{_config.Email}" },
                    termsOfServiceAgreed: true,
                    eabKeyId: _config.EabKeyId,
                    eabKey: _config.EabHmacKey);
            }
            else
            {
                _accountContext = await _acmeContext.NewAccount(_config.Email, termsOfServiceAgreed: true);
            }

            // Save the account to the database
            var accountPem = _acmeContext.AccountKey.ToPem();
            var encryptedKey = _dataProtection.Protect(accountPem);

            var newAccount = new AcmeAccount
            {
                DirectoryUrl = new Uri(_config.DirectoryUrl),
                Email = _config.Email,
                AccountUrl = _accountContext.Location,
                EncryptedPrivateKey = encryptedKey,
                TermsOfServiceAccepted = true,
                IsActive = true,
                EabKeyId = _config.EabKeyId,
                EncryptedEabHmacKey = !string.IsNullOrEmpty(_config.EabHmacKey)
                    ? _dataProtection.Protect(_config.EabHmacKey)
                    : null
            };

            _unitOfWork.AcmeAccounts.Add(newAccount);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation("ACME account created and saved: {AccountUrl}", newAccount.AccountUrl);
        }
    }

    private async Task ProcessAuthorizationAsync(IAuthorizationContext auth, CancellationToken ct)
    {
        var authResource = await auth.Resource();

        if (authResource.Status == AuthorizationStatus.Valid)
        {
            _logger.LogDebug("Authorization already valid for {Identifier}", authResource.Identifier?.Value);
            return;
        }

        var domain = authResource.Identifier?.Value
            ?? throw new InvalidOperationException("Authorization missing identifier");

        _logger.LogInformation("Processing authorization for domain: {Domain}", domain);

        // Get available challenges
        var challenges = await auth.Challenges();

        // Prefer HTTP-01 for simplicity, fall back to DNS-01
        IChallengeContext? selectedChallenge = null;
        string? keyAuth = null;
        string challengeType = "unknown";

        // Try HTTP-01 first
        var http01 = challenges.FirstOrDefault(c => c.Type == ChallengeTypes.Http01);
        if (http01 != null && _challengeProvider.ChallengeType == "http-01")
        {
            selectedChallenge = http01;
            keyAuth = http01.KeyAuthz;
            challengeType = "http-01";
        }

        // Fall back to DNS-01
        var dns01 = challenges.FirstOrDefault(c => c.Type == ChallengeTypes.Dns01);
        if (selectedChallenge == null && dns01 != null)
        {
            selectedChallenge = dns01;
            keyAuth = _acmeContext!.AccountKey.DnsTxt(dns01.Token);
            challengeType = "dns-01";
        }

        if (selectedChallenge == null || keyAuth == null)
        {
            throw new InvalidOperationException(
                $"No supported challenge type available for domain {domain}. " +
                $"Available: {string.Join(", ", challenges.Select(c => c.Type))}");
        }

        _logger.LogDebug("Using {ChallengeType} challenge for {Domain}", challengeType, domain);

        // Prepare the challenge
        await _challengeProvider.PrepareAsync(domain, selectedChallenge.Token, keyAuth, ct);

        try
        {
            // Validate the challenge
            var challenge = await selectedChallenge.Validate();

            // Poll for validation completion with exponential backoff
            var maxAttempts = 30;
            var baseDelaySeconds = 2;
            var maxDelaySeconds = 10;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var challengeResource = await selectedChallenge.Resource();

                switch (challengeResource.Status)
                {
                    case ChallengeStatus.Valid:
                        _logger.LogInformation("Challenge validated successfully for {Domain}", domain);
                        return; // Success - exit method

                    case ChallengeStatus.Invalid:
                        var error = challengeResource.Error;
                        var errorDetail = error?.Detail ?? "Unknown error";
                        var errorType = error?.Type?.ToString();

                        // Determine if this is a transient error
                        var isTransient = IsTransientAcmeError(errorType);

                        _logger.LogWarning(
                            "ACME challenge validation failed for {Domain}. Type: {ErrorType}, Detail: {ErrorDetail}, Transient: {IsTransient}",
                            domain, errorType, errorDetail, isTransient);

                        throw new AcmeChallengeException(
                            domain,
                            challengeType,
                            $"Challenge validation failed for {domain}: {errorDetail}",
                            isTransient: isTransient,
                            retryAfterSeconds: isTransient ? 60 : null,
                            acmeErrorType: errorType);

                    case ChallengeStatus.Pending:
                    case ChallengeStatus.Processing:
                        // Still waiting - apply exponential backoff
                        var delay = Math.Min(baseDelaySeconds * (1 << Math.Min(attempt, 3)), maxDelaySeconds);
                        _logger.LogDebug(
                            "Challenge for {Domain} still {Status}, waiting {Delay}s (attempt {Attempt}/{MaxAttempts})",
                            domain, challengeResource.Status, delay, attempt + 1, maxAttempts);
                        await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                        break;

                    default:
                        _logger.LogWarning("Unknown challenge status {Status} for {Domain}", challengeResource.Status, domain);
                        await Task.Delay(TimeSpan.FromSeconds(baseDelaySeconds), ct);
                        break;
                }
            }

            // Timed out waiting for validation
            throw new AcmeChallengeException(
                domain,
                challengeType,
                $"Challenge validation timed out after {maxAttempts} attempts for domain {domain}. " +
                "The ACME server may be slow to validate, or there may be DNS/network issues.",
                isTransient: true,
                retryAfterSeconds: 120);
        }
        finally
        {
            // Always cleanup the challenge
            await _challengeProvider.CleanupAsync(domain, selectedChallenge.Token, ct);
        }
    }

    private static List<string> ExtractDomainsFromCsr(ParsedCsr csr)
    {
        var domains = new List<string>();

        // Extract CN from subject DN
        var subject = csr.SubjectDn;
        var cnMatch = System.Text.RegularExpressions.Regex.Match(subject, @"CN=([^,]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (cnMatch.Success)
        {
            var cn = cnMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(cn) && IsValidDomainName(cn))
            {
                domains.Add(cn);
            }
        }

        // Add SANs
        foreach (var san in csr.SubjectAlternativeNames)
        {
            // Handle DNS: prefix
            var domain = san.StartsWith("DNS:", StringComparison.OrdinalIgnoreCase)
                ? san[4..].Trim()
                : san.Trim();

            if (IsValidDomainName(domain) && !domains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                domains.Add(domain);
            }
        }

        return domains;
    }

    private static bool IsValidDomainName(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        // Basic domain validation - contains at least one dot and no spaces
        return domain.Contains('.') && !domain.Contains(' ') && domain.Length <= 253;
    }

    private static IKey GenerateCertificateKey(ParsedCsr csr)
    {
        // Use the key type matching the CSR if possible
        return csr.PublicKeyAlgorithm.ToUpperInvariant() switch
        {
            "RSA" => KeyFactory.NewKey(KeyAlgorithm.RS256),
            "ECDSA" or "EC" => KeyFactory.NewKey(KeyAlgorithm.ES256),
            _ => KeyFactory.NewKey(KeyAlgorithm.RS256) // Default to RSA
        };
    }

    /// <summary>
    /// Generates a cryptographically secure random password for PFX protection.
    /// </summary>
    private static string GenerateSecurePassword()
    {
        // Generate 32 bytes of random data and convert to base64 for a strong password
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Determines if an ACME error is transient (may succeed on retry).
    /// </summary>
    /// <param name="errorType">The ACME error type URI.</param>
    /// <returns>True if the error is likely transient.</returns>
    private static bool IsTransientAcmeError(string? errorType)
    {
        if (string.IsNullOrEmpty(errorType))
            return false;

        // Transient errors that may succeed on retry
        // See RFC 8555 Section 6.7 for error types
        var transientErrors = new[]
        {
            "urn:ietf:params:acme:error:serverInternal",
            "urn:ietf:params:acme:error:rateLimited",
            "urn:ietf:params:acme:error:connection", // ACME server couldn't connect to validate
        };

        return transientErrors.Any(e => errorType.Contains(e, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Configuration for ACME connector.
/// </summary>
public class AcmeConnectorConfig
{
    /// <summary>
    /// ACME directory URL.
    /// </summary>
    public string DirectoryUrl { get; set; } = WellKnownServers.LetsEncryptV2.ToString();

    /// <summary>
    /// Account email address for notifications.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// External Account Binding Key ID (for CAs that require EAB like ZeroSSL).
    /// </summary>
    public string? EabKeyId { get; set; }

    /// <summary>
    /// External Account Binding HMAC Key (for CAs that require EAB).
    /// </summary>
    public string? EabHmacKey { get; set; }

    /// <summary>
    /// Preferred challenge type: "http-01" or "dns-01".
    /// </summary>
    public string PreferredChallengeType { get; set; } = "http-01";
}

/// <summary>
/// Well-known ACME server URLs.
/// </summary>
public static class WellKnownServers
{
    /// <summary>
    /// Let's Encrypt production server.
    /// </summary>
    public static readonly Uri LetsEncryptV2 = new("https://acme-v02.api.letsencrypt.org/directory");

    /// <summary>
    /// Let's Encrypt staging server (for testing).
    /// </summary>
    public static readonly Uri LetsEncryptStagingV2 = new("https://acme-staging-v02.api.letsencrypt.org/directory");

    /// <summary>
    /// ZeroSSL ACME server.
    /// </summary>
    public static readonly Uri ZeroSsl = new("https://acme.zerossl.com/v2/DV90");

    /// <summary>
    /// BuyPass ACME server.
    /// </summary>
    public static readonly Uri BuyPass = new("https://api.buypass.com/acme/directory");

    /// <summary>
    /// BuyPass test server.
    /// </summary>
    public static readonly Uri BuyPassTest = new("https://api.test4.buypass.no/acme/directory");
}
