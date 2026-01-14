using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Acme;

namespace RTSec.Kryptonian.Infrastructure.Crypto;

/// <summary>
/// Factory for creating CA connectors based on backend type.
/// Caches connectors to avoid repeated initialization.
/// </summary>
public class CaConnectorFactory : ICaConnectorFactory, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataProtectionService _dataProtection;
    private readonly Http01ChallengeStore _http01ChallengeStore;
    private readonly Dns01ChallengeStub _dns01ChallengeStub;
    private readonly Dictionary<Guid, ICaConnector> _connectorCache = new();
    private readonly object _lock = new();
    private bool _disposed;

    public CaConnectorFactory(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IUnitOfWork unitOfWork,
        IDataProtectionService dataProtection,
        Http01ChallengeStore http01ChallengeStore,
        Dns01ChallengeStub dns01ChallengeStub)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _unitOfWork = unitOfWork;
        _dataProtection = dataProtection;
        _http01ChallengeStore = http01ChallengeStore;
        _dns01ChallengeStub = dns01ChallengeStub;
    }

    /// <inheritdoc />
    public ICaConnector CreateConnector(CaBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);

        lock (_lock)
        {
            // Return cached connector if available
            if (_connectorCache.TryGetValue(backend.Id, out var cached))
                return cached;

            var connector = backend.Type switch
            {
                // Implemented
                CaBackendType.SelfSigned => CreateSelfSignedConnector(backend),
                CaBackendType.Acme => CreateAcmeConnector(backend),

                // Planned - see README.md for roadmap
                CaBackendType.Adcs => throw new NotSupportedException("Microsoft ADCS connector not yet implemented. See README.md for roadmap."),
                CaBackendType.Ejbca => throw new NotSupportedException("EJBCA connector not yet implemented. See README.md for roadmap."),
                CaBackendType.Cfssl => throw new NotSupportedException("CFSSL connector not yet implemented. See README.md for roadmap."),
                CaBackendType.HashiCorpVault => throw new NotSupportedException("HashiCorp Vault connector not yet implemented. See README.md for roadmap."),
                CaBackendType.Smallstep => throw new NotSupportedException("Smallstep connector not yet implemented. See README.md for roadmap."),
                CaBackendType.OpenXpki => throw new NotSupportedException("OpenXPKI connector not yet implemented. See README.md for roadmap."),

                _ => throw new ArgumentException($"Unknown CA backend type: {backend.Type}", nameof(backend))
            };

            _connectorCache[backend.Id] = connector;
            return connector;
        }
    }

    /// <inheritdoc />
    public ICaConnector? GetCachedConnector(Guid backendId)
    {
        lock (_lock)
        {
            return _connectorCache.TryGetValue(backendId, out var connector) ? connector : null;
        }
    }

    private ICaConnector CreateSelfSignedConnector(CaBackend backend)
    {
        var logger = _loggerFactory.CreateLogger<SelfSignedCaConnector>();

        // Get certificate path from config or backend config
        var certPath = GetConfigValue(backend, "CertPath", "KRYPTONIAN__CA__SELFSIGNED__CERTPATH");
        var keyPath = GetConfigValue(backend, "KeyPath", "KRYPTONIAN__CA__SELFSIGNED__KEYPATH");
        var pfxPath = GetConfigValue(backend, "PfxPath", "KRYPTONIAN__CA__SELFSIGNED__PFXPATH");
        var pfxPassword = GetConfigValue(backend, "PfxPassword", "KRYPTONIAN__CA__SELFSIGNED__PFXPASSWORD");

        X509Certificate2 caCert;

        if (!string.IsNullOrEmpty(pfxPath) && File.Exists(pfxPath))
        {
            // Require password for PFX files to enforce key protection
            if (string.IsNullOrEmpty(pfxPassword))
            {
                throw new InvalidOperationException(
                    "PFX password is required for self-signed CA. " +
                    "Set via environment variable KRYPTONIAN__CA__SELFSIGNED__PFXPASSWORD or backend config.");
            }

            // Load from PFX - Use EphemeralKeySet on non-Windows to avoid key storage issues,
            // and MachineKeySet on Windows for better key protection. Avoid Exportable flag
            // unless the downstream dependency truly requires it (BouncyCastle needs the private key).
            var keyStorageFlags = OperatingSystem.IsWindows()
                ? X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet
                : X509KeyStorageFlags.EphemeralKeySet;

            // Note: We need to export the private key for BouncyCastle's signing operations.
            // On Windows with MachineKeySet, the key can still be extracted by the current process.
            // For additional hardening in production, consider using HSM-backed keys.
            caCert = new X509Certificate2(pfxPath, pfxPassword, keyStorageFlags);

            // Verify the certificate has a private key accessible for signing
            if (!caCert.HasPrivateKey)
            {
                throw new InvalidOperationException("CA certificate must have an accessible private key for signing.");
            }
        }
        else if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(keyPath))
        {
            // Load from separate PEM files
            caCert = LoadCertificateFromPem(certPath, keyPath);
        }
        else
        {
            throw new InvalidOperationException(
                "Self-signed CA requires either PfxPath or both CertPath and KeyPath to be configured. " +
                "Set via environment variables KRYPTONIAN__CA__SELFSIGNED__* or backend config.");
        }

        return new SelfSignedCaConnector(logger, caCert);
    }

    private ICaConnector CreateAcmeConnector(CaBackend backend)
    {
        var logger = _loggerFactory.CreateLogger<AcmeCaConnector>();

        // Get ACME configuration from backend config or environment
        var directoryUrl = GetConfigValue(backend, "DirectoryUrl", "KRYPTONIAN__ACME__DIRECTORYURL")
            ?? backend.Url
            ?? WellKnownServers.LetsEncryptV2.ToString();

        var email = GetConfigValue(backend, "Email", "KRYPTONIAN__ACME__EMAIL")
            ?? throw new InvalidOperationException(
                "ACME connector requires an email address. " +
                "Set via backend config 'Email' or environment variable KRYPTONIAN__ACME__EMAIL.");

        var eabKeyId = GetConfigValue(backend, "EabKeyId", "KRYPTONIAN__ACME__EABKEYID");
        var eabHmacKey = GetConfigValue(backend, "EabHmacKey", "KRYPTONIAN__ACME__EABHMACKEY");
        var preferredChallenge = GetConfigValue(backend, "PreferredChallengeType", "KRYPTONIAN__ACME__CHALLENGETYPE") ?? "http-01";

        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = directoryUrl,
            Email = email,
            EabKeyId = eabKeyId,
            EabHmacKey = eabHmacKey,
            PreferredChallengeType = preferredChallenge
        };

        // Select challenge provider based on configuration
        IAcmeChallengeProvider challengeProvider = preferredChallenge.ToLowerInvariant() switch
        {
            "dns-01" => _dns01ChallengeStub,
            _ => _http01ChallengeStore // Default to HTTP-01
        };

        _loggerFactory.CreateLogger<CaConnectorFactory>()
            .LogInformation("ACME backend {BackendId} using {ChallengeType} challenge provider",
                backend.Id, challengeProvider.ChallengeType);

        return new AcmeCaConnector(logger, _unitOfWork, challengeProvider, _dataProtection, config);
    }

    private string? GetConfigValue(CaBackend backend, string configKey, string envKey)
    {
        // First check backend-specific config
        if (backend.Config?.TryGetValue(configKey, out var value) == true)
        {
            // Handle direct string values
            if (value is string strValue)
                return strValue;

            // Handle JsonElement values (from EnableDynamicJson deserialization)
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    return jsonElement.GetString();
                // For other types, convert to string representation
                return jsonElement.ToString();
            }

            // Fallback for other types
            return value?.ToString();
        }

        // Then check configuration (which includes environment variables)
        var envValue = _configuration[envKey.Replace("__", ":")];
        if (!string.IsNullOrEmpty(envValue))
            return envValue;

        // Try direct environment variable
        return Environment.GetEnvironmentVariable(envKey);
    }

    private static X509Certificate2 LoadCertificateFromPem(string certPath, string keyPath)
    {
        if (!File.Exists(certPath))
            throw new FileNotFoundException($"CA certificate file not found: {certPath}");

        if (!File.Exists(keyPath))
            throw new FileNotFoundException($"CA private key file not found: {keyPath}");

        var certPem = File.ReadAllText(certPath);
        var keyPem = File.ReadAllText(keyPath);

        // Use .NET 6+ PEM import
        var cert = X509Certificate2.CreateFromPem(certPem, keyPem);

        // Use EphemeralKeySet on non-Windows to avoid key storage issues,
        // and MachineKeySet on Windows for better key protection.
        var keyStorageFlags = OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet
            : X509KeyStorageFlags.EphemeralKeySet;

        // Re-import to ensure proper key storage. On Linux with EphemeralKeySet,
        // the key remains in memory and is cleaned up when the certificate is disposed.
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, keyStorageFlags);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            foreach (var connector in _connectorCache.Values)
            {
                if (connector is IDisposable disposable)
                    disposable.Dispose();
            }
            _connectorCache.Clear();
        }

        _disposed = true;
    }
}
