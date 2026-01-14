using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Infrastructure.Acme;

/// <summary>
/// In-memory store for HTTP-01 ACME challenges.
/// Challenges are stored temporarily during the validation process.
/// </summary>
public class Http01ChallengeStore : IAcmeChallengeProvider
{
    private readonly ILogger<Http01ChallengeStore> _logger;
    private readonly ConcurrentDictionary<string, ChallengeData> _challenges = new();

    public string ChallengeType => "http-01";

    public Http01ChallengeStore(ILogger<Http01ChallengeStore> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task PrepareAsync(string domain, string token, string keyAuthorization, CancellationToken ct = default)
    {
        var challengeData = new ChallengeData
        {
            Domain = domain,
            Token = token,
            KeyAuthorization = keyAuthorization,
            CreatedAt = DateTime.UtcNow
        };

        _challenges[token] = challengeData;

        _logger.LogInformation("HTTP-01 challenge prepared for domain {Domain}, token: {Token}",
            domain, token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CleanupAsync(string domain, string token, CancellationToken ct = default)
    {
        if (_challenges.TryRemove(token, out _))
        {
            _logger.LogDebug("HTTP-01 challenge cleaned up for domain {Domain}, token: {Token}",
                domain, token);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the key authorization for a given token.
    /// Used by the challenge endpoint to respond to ACME server validation.
    /// </summary>
    /// <param name="token">The challenge token.</param>
    /// <returns>The key authorization, or null if not found.</returns>
    public string? GetKeyAuthorization(string token)
    {
        if (_challenges.TryGetValue(token, out var data))
        {
            return data.KeyAuthorization;
        }

        return null;
    }

    /// <summary>
    /// Removes expired challenges (older than 10 minutes).
    /// </summary>
    public void CleanupExpired()
    {
        var expiredTokens = _challenges
            .Where(kvp => kvp.Value.CreatedAt < DateTime.UtcNow.AddMinutes(-10))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var token in expiredTokens)
        {
            if (_challenges.TryRemove(token, out var data))
            {
                _logger.LogDebug("Removed expired HTTP-01 challenge for domain {Domain}", data.Domain);
            }
        }
    }

    private class ChallengeData
    {
        public string Domain { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
        public string KeyAuthorization { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }
}
