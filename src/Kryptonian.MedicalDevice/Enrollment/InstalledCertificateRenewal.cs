using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Kryptonian.MedicalDevice.Enrollment;

/// <summary>Renews the user-installed credential while the device app is running.</summary>
public sealed class InstalledCertificateRenewal
{
    private readonly string _statePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public InstalledCertificateRenewal(string statePath) => _statePath = statePath;

    public async Task TrackAsync(Uri gateway, X509Certificate2 installedCertificate, CancellationToken ct = default)
    {
        if (gateway.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Renewal requires HTTPS.", nameof(gateway));
        await _gate.WaitAsync(ct);
        try { await SaveAsync(new RenewalState(gateway.AbsoluteUri, installedCertificate.Thumbprint), ct); }
        finally { _gate.Release(); }
    }

    public static bool IsDue(DateTime now, DateTime notBefore, DateTime notAfter) =>
        now < notAfter && now >= notBefore.AddTicks((notAfter - notBefore).Ticks * 2 / 3);

    public async Task RunAsync(Action<string> report, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        var nextAttempt = DateTime.MinValue;
        try
        {
            do
            {
                if (DateTime.UtcNow < nextAttempt || !File.Exists(_statePath)) continue;
                await _gate.WaitAsync(ct);
                try
                {
                    var state = JsonSerializer.Deserialize<RenewalState>(await File.ReadAllTextAsync(_statePath, ct))
                        ?? throw new InvalidDataException("Renewal state is empty.");
                    var gateway = new Uri(state.Gateway);
                    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    store.Open(OpenFlags.ReadWrite);
                    var matches = store.Certificates.Find(X509FindType.FindByThumbprint, state.Thumbprint, false);
                    try
                    {
                        if (matches.Count != 1 || !matches[0].HasPrivateKey)
                            throw new InvalidOperationException("Managed certificate/private key is unavailable; enroll again.");
                        var current = matches[0];
                        if (current.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                            throw new InvalidOperationException("Managed certificate expired; attended activation is required.");
                        if (!IsDue(DateTime.UtcNow, current.NotBefore.ToUniversalTime(), current.NotAfter.ToUniversalTime())) continue;
                        var result = await EstEnrollmentClient.ReenrollAsync(gateway, current, current.Subject, ct);
                        using var renewed = result.Certificate;
                        try
                        {
                            var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                            using var persisted = X509CertificateLoader.LoadPkcs12(renewed.Export(X509ContentType.Pfx, password), password,
                                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            store.Add(persisted);
                            // Persist the new selection only after installation succeeds.
                            await SaveAsync(state with { Thumbprint = renewed.Thumbprint }, ct);
                            report($"Automatically renewed installed certificate: {renewed.Thumbprint}");
                            // ponytail: retain the previous credential for overlap; a long-running
                            // installation needs an explicit retirement/session-drain policy.
                        }
                        finally { foreach (var cert in result.IssuedCertificates) cert.Dispose(); }
                    }
                    finally { foreach (var cert in matches) cert.Dispose(); }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    nextAttempt = DateTime.UtcNow.AddMinutes(5);
                    report($"Automatic renewal failed; retry in five minutes: {ex.Message}");
                }
                finally { _gate.Release(); }
            } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task SaveAsync(RenewalState state, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_statePath))!);
        var temporary = _statePath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state), ct);
        File.Move(temporary, _statePath, overwrite: true);
    }

    private sealed record RenewalState(string Gateway, string Thumbprint);
}
