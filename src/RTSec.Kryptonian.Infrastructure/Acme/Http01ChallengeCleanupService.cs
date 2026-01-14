using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RTSec.Kryptonian.Infrastructure.Acme;

/// <summary>
/// Background service that periodically cleans up expired HTTP-01 challenge tokens.
/// Tokens older than 10 minutes are removed to prevent memory accumulation.
///
/// Note: HTTP-01 challenge tokens are stored in-memory only. If the application restarts
/// during an ACME validation, the tokens will be lost and validation will fail.
/// For high-availability scenarios, consider using a distributed cache (Redis, etc.).
/// </summary>
public class Http01ChallengeCleanupService : BackgroundService
{
    private readonly Http01ChallengeStore _challengeStore;
    private readonly ILogger<Http01ChallengeCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public Http01ChallengeCleanupService(
        Http01ChallengeStore challengeStore,
        ILogger<Http01ChallengeCleanupService> logger)
    {
        _challengeStore = challengeStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HTTP-01 challenge cleanup service started. Cleanup interval: {Interval}",
            _cleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                _logger.LogDebug("Running HTTP-01 challenge token cleanup");
                _challengeStore.CleanupExpired();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during HTTP-01 challenge cleanup");
            }
        }

        _logger.LogInformation("HTTP-01 challenge cleanup service stopped");
    }
}
