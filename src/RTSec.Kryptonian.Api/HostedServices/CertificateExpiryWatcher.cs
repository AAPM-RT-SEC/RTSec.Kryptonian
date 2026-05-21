using Microsoft.Extensions.DependencyInjection;
using RTSec.Kryptonian.Application.Notifications;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Api.HostedServices;

/// <summary>
/// Scans daily for device certificates whose NotAfter is within the configured
/// warning window and that have not already been notified about. Fires a per-cert
/// notification through <see cref="INotificationDispatcher"/> and records the
/// timestamp on the certificate so the same warning isn't sent every day.
///
/// Runs entirely in-process; no external scheduler. Safe on on-prem hosts.
/// </summary>
public class CertificateExpiryWatcher : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(24);

    /// <summary>How long to wait before re-notifying about the same certificate.</summary>
    private static readonly TimeSpan RenotifyAfter = TimeSpan.FromDays(7);

    /// <summary>Initial delay so we don't fire during cold-start migrations.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CertificateExpiryWatcher> _logger;

    public CertificateExpiryWatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<CertificateExpiryWatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Certificate expiry watcher started (scan interval {Interval})", ScanInterval);

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Certificate expiry scan failed");
            }

            try
            {
                await Task.Delay(ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Certificate expiry watcher stopped");
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var settings = await uow.NotificationSettings.GetSingletonAsync(ct);
        if (settings is null || !settings.Enabled || !settings.NotifyOnCertificateNearExpiry)
        {
            _logger.LogDebug("Expiry notifications disabled; skipping scan");
            return;
        }

        var now = DateTime.UtcNow;
        var horizon = now.AddDays(settings.ExpiryWarningDays);
        var expiringSoon = await uow.Certificates.GetExpiringAsync(horizon, ct);

        var notifiedCount = 0;
        foreach (var cert in expiringSoon)
        {
            if (cert.Status != CertificateStatus.Valid) continue;
            if (cert.NotAfter <= now) continue; // already expired — different concern
            if (await HasRequestedRenewalAsync(cert, uow, ct)) continue;
            if (cert.LastExpiryNotifiedAt is { } last && (now - last) < RenotifyAfter) continue;

            var daysLeft = (int)Math.Ceiling((cert.NotAfter - now).TotalDays);

            await dispatcher.NotifyCertificateNearExpiryAsync(
                new CertificateExpiryContext(
                    SerialNumber: cert.SerialNumber,
                    SubjectDn: cert.SubjectDn,
                    DeviceId: cert.DeviceId,
                    NotAfterUtc: cert.NotAfter,
                    DaysUntilExpiry: daysLeft),
                ct);

            cert.LastExpiryNotifiedAt = now;
            uow.Certificates.Update(cert);
            notifiedCount++;
        }

        if (notifiedCount > 0)
        {
            await uow.SaveChangesAsync(ct);
            _logger.LogInformation("Notified about {Count} expiring certificate(s)", notifiedCount);
        }
    }

    /// <summary>
    /// A device "has requested renewal" when its <c>LastCertificateId</c> already points at
    /// a different (newer) certificate than the one we're inspecting. Device.LastCertificateId
    /// is updated on every successful enrollment.
    /// </summary>
    private static async Task<bool> HasRequestedRenewalAsync(Certificate cert, IUnitOfWork uow, CancellationToken ct)
    {
        if (cert.DeviceRecordId is not { } deviceRecordId)
        {
            return false;
        }

        var device = await uow.Devices.GetByIdAsync(deviceRecordId, ct);
        return device?.LastCertificateId is { } latestId && latestId != cert.Id;
    }
}
