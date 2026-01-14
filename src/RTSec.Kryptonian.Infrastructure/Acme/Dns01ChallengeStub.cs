using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Infrastructure.Acme;

/// <summary>
/// Stub implementation of DNS-01 challenge provider.
/// Logs the required DNS TXT record for manual configuration.
///
/// In production, this would be replaced with provider-specific implementations
/// (Cloudflare, Route53, Azure DNS, etc.) that automatically create/delete records.
///
/// To use DNS-01 challenges with this stub:
/// 1. Configure PreferredChallengeType = "dns-01" on the ACME backend
/// 2. When a certificate is requested, check logs for the TXT record value
/// 3. Manually create the TXT record at _acme-challenge.{domain}
/// 4. The ACME server will validate and issue the certificate
/// 5. Remove the TXT record after issuance
/// </summary>
public class Dns01ChallengeStub : IAcmeChallengeProvider
{
    private readonly ILogger<Dns01ChallengeStub> _logger;

    public string ChallengeType => "dns-01";

    public Dns01ChallengeStub(ILogger<Dns01ChallengeStub> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task PrepareAsync(string domain, string token, string keyAuthorization, CancellationToken ct = default)
    {
        // Compute the DNS TXT record value (base64url-encoded SHA256 of key authorization)
        // This is the value that must be placed at _acme-challenge.{domain}
        var txtRecordValue = ComputeDnsTxtValue(keyAuthorization);

        _logger.LogWarning(
            "DNS-01 challenge requires manual DNS configuration:\n" +
            "  Domain: {Domain}\n" +
            "  Record Name: _acme-challenge.{Domain}\n" +
            "  Record Type: TXT\n" +
            "  Record Value: {TxtValue}\n" +
            "  Token: {Token}\n\n" +
            "Create this TXT record in your DNS provider, then wait for propagation.",
            domain, domain, txtRecordValue, token);

        // Don't throw - allow the challenge to proceed
        // The ACME server will validate and fail if the record isn't present
        // This allows manual intervention scenarios
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CleanupAsync(string domain, string token, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "DNS-01 cleanup: Please remove the TXT record at _acme-challenge.{Domain}",
            domain);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Computes the DNS TXT record value for a DNS-01 challenge.
    /// The value is the base64url-encoded SHA256 hash of the key authorization.
    /// </summary>
    private static string ComputeDnsTxtValue(string keyAuthorization)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyAuthorization));
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Base64url encoding (RFC 4648) without padding.
    /// </summary>
    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
