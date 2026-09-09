using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RTSec.Kryptonian.Api;

internal sealed record SandboxBootstrap(
    string DataDirectory,
    string DeviceCaPemPath,
    string ServerPfxPath,
    string ServerPfxPassword,
    string AdminPfxPath,
    string AdminPfxPassword,
    int AdminPort,
    int EstPort,
    int CrlPort,
    IReadOnlyDictionary<string, string?> Configuration)
{
    private const string SecretsFile = "secrets.json";
    private static readonly string[] RequiredFiles =
    [
        SecretsFile, "ca.pfx", "ca.pem", "server.pfx",
        "admin-ca.pfx", "admin-ca.pem", "admin.pfx"
    ];

    internal static SandboxBootstrap Initialize(IConfiguration configuration)
    {
        var directory = Path.GetFullPath(configuration["Kryptonian:Sandbox:DataDirectory"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KryptonianSandbox"));
        var adminPort = GetPort(configuration, "Kryptonian:Sandbox:AdminPort", 7445);
        var estPort = GetPort(configuration, "Kryptonian:Sandbox:EstPort", 7443);
        var crlPort = GetPort(configuration, "Kryptonian:Sandbox:CrlPort", 7444);
        if (new[] { adminPort, estPort, crlPort }.Distinct().Count() != 3)
            throw new InvalidOperationException("Sandbox admin, EST, and CRL ports must be distinct.");

        RejectReparsePoint(directory);
        Directory.CreateDirectory(directory);
        RejectReparsePoint(directory);
        RestrictDirectory(directory);
        var paths = RequiredFiles.Select(file => Path.Combine(directory, file)).ToArray();
        var present = paths.Count(File.Exists);
        if (present != 0 && present != paths.Length)
            throw new InvalidOperationException("Sandbox state is incomplete; refusing to regenerate certificate authority material.");

        var keysDirectory = Path.Combine(directory, "dataprotection");
        RejectReparsePoint(keysDirectory);
        var hasDataProtectionState = Directory.Exists(keysDirectory)
            && Directory.EnumerateFileSystemEntries(keysDirectory).Any();
        Directory.CreateDirectory(keysDirectory);
        if (present == 0 && (hasDataProtectionState || File.Exists(Path.Combine(directory, "kryptonian.db"))))
            throw new InvalidOperationException("Sandbox data protection state is incomplete; refusing to regenerate certificate authority material.");
        if (present == 0)
            CreateState(directory);

        var secrets = LoadSecrets(Path.Combine(directory, SecretsFile));
        ValidateState(directory, secrets);
        var caPfx = Path.Combine(directory, "ca.pfx");
        var caPem = Path.Combine(directory, "ca.pem");
        var serverPfx = Path.Combine(directory, "server.pfx");
        var adminPfx = Path.Combine(directory, "admin.pfx");
        var config = new Dictionary<string, string?>
        {
            ["Kryptonian:Sandbox:Enabled"] = "true",
            ["Kryptonian:Database:Provider"] = "Sqlite",
            ["Kryptonian:Database:ConnectionString"] = $"Data Source={Path.Combine(directory, "kryptonian.db")}",
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(directory, "kryptonian.db")}",
            ["Kryptonian:Auth:JwtSecret"] = secrets.JwtSecret,
            ["Kryptonian:AdminApi:ApiKeys"] = secrets.AdminApiKey,
            ["Kryptonian:Auth:InitialAdminUsername"] = string.Empty,
            ["Kryptonian:Auth:InitialAdminPassword"] = string.Empty,
            ["Kryptonian:Auth:InitialAdminEmail"] = string.Empty,
            ["Kryptonian:Ca:SelfSigned:PfxPath"] = caPfx,
            ["Kryptonian:Ca:SelfSigned:PfxPassword"] = secrets.CaPfxPassword,
            ["Kryptonian:Ca:SelfSigned:CrlDistributionPointUrl"] = $"http://localhost:{crlPort}/api/crl/{{issuer-SHA256}}.crl",
            ["Kryptonian:Tls:ClientCaFiles:0"] = caPem,
            ["Kryptonian:Tls:CrlOnlyHttp"] = "true",
            ["Kryptonian:Tls:RedirectHttpToHttps"] = "false",
            ["Kestrel:Certificates:Default:Path"] = serverPfx,
            ["Kestrel:Certificates:Default:Password"] = secrets.PfxPassword,
            ["Kryptonian:Sandbox:LogDirectory"] = Path.Combine(directory, "logs"),
            ["Kryptonian:Sandbox:DataProtectionDirectory"] = keysDirectory
        };
        return new SandboxBootstrap(directory, caPem, serverPfx, secrets.PfxPassword, adminPfx, secrets.PfxPassword,
            adminPort, estPort, crlPort, config);
    }

    internal static bool AllowsRequest(int port, int adminPort, int estPort, int crlPort, string path, string method)
    {
        if (port == estPort) return IsPathOrChild(path, "/.well-known/est");
        if (port == crlPort) return (method is "GET" or "HEAD") && IsPathOrChild(path, "/api/crl");
        return port == adminPort && (IsPathOrChild(path, "/api") || !path.StartsWith("/.well-known", StringComparison.OrdinalIgnoreCase));
    }

    private static int GetPort(IConfiguration config, string key, int defaultValue)
    {
        var port = config.GetValue<int?>(key) ?? defaultValue;
        if (port is < 1 or > 65535) throw new InvalidOperationException($"{key} must be a TCP port.");
        return port;
    }

    private static void CreateState(string directory)
    {
        var secrets = new SandboxSecrets(NewSecret(), NewSecret(), NewSecret(), NewSecret());
        File.WriteAllText(Path.Combine(directory, SecretsFile), JsonSerializer.Serialize(secrets, JsonOptions));
        CreateCa(Path.Combine(directory, "ca.pfx"), Path.Combine(directory, "ca.pem"), secrets.CaPfxPassword, "Kryptonian Sandbox Device CA");
        CreateCa(Path.Combine(directory, "admin-ca.pfx"), Path.Combine(directory, "admin-ca.pem"), secrets.PfxPassword, "Kryptonian Sandbox Admin CA");
        CreateLeaf(Path.Combine(directory, "ca.pfx"), secrets.CaPfxPassword, Path.Combine(directory, "server.pfx"), secrets.PfxPassword, "localhost");
        CreateLeaf(Path.Combine(directory, "admin-ca.pfx"), secrets.PfxPassword, Path.Combine(directory, "admin.pfx"), secrets.PfxPassword, "localhost");
    }

    private static SandboxSecrets LoadSecrets(string path)
    {
        try
        {
            var secrets = JsonSerializer.Deserialize<SandboxSecrets>(File.ReadAllText(path), JsonOptions);
            if (secrets is null || new[] { secrets.CaPfxPassword, secrets.PfxPassword, secrets.JwtSecret, secrets.AdminApiKey }.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException();
            return secrets;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            throw new InvalidOperationException("Sandbox secrets state is corrupt.", ex);
        }
    }

    private static void ValidateState(string directory, SandboxSecrets secrets)
    {
        foreach (var file in RequiredFiles)
        {
            RejectReparsePoint(Path.Combine(directory, file));
            if (!File.Exists(Path.Combine(directory, file)))
                throw new InvalidOperationException("Sandbox state is incomplete; refusing to regenerate certificate authority material.");
        }

        try
        {
            using var deviceCa = LoadPfx(Path.Combine(directory, "ca.pfx"), secrets.CaPfxPassword, X509KeyStorageFlags.Exportable);
            using var adminCa = LoadPfx(Path.Combine(directory, "admin-ca.pfx"), secrets.PfxPassword, X509KeyStorageFlags.Exportable);
            using var server = LoadPfx(Path.Combine(directory, "server.pfx"), secrets.PfxPassword, X509KeyStorageFlags.Exportable);
            using var admin = LoadPfx(Path.Combine(directory, "admin.pfx"), secrets.PfxPassword, X509KeyStorageFlags.Exportable);
            if (!deviceCa.HasPrivateKey || !adminCa.HasPrivateKey || !server.HasPrivateKey || !admin.HasPrivateKey)
                throw new CryptographicException("A sandbox certificate key is unavailable.");
            using var devicePem = X509Certificate2.CreateFromPem(File.ReadAllText(Path.Combine(directory, "ca.pem")));
            using var adminPem = X509Certificate2.CreateFromPem(File.ReadAllText(Path.Combine(directory, "admin-ca.pem")));
            if (!deviceCa.RawData.SequenceEqual(devicePem.RawData) || !adminCa.RawData.SequenceEqual(adminPem.RawData))
                throw new CryptographicException("A sandbox CA PEM does not match its PFX.");
            ValidateLeaf(deviceCa, server);
            ValidateLeaf(adminCa, admin);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            throw new InvalidOperationException("Sandbox certificate state is corrupt.", ex);
        }
    }

    private static void CreateCa(string pfxPath, string pemPath, string password, string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={subject}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        File.WriteAllBytes(pfxPath, certificate.Export(X509ContentType.Pfx, password));
        File.WriteAllText(pemPath, certificate.ExportCertificatePem());
    }

    internal static X509Certificate2 LoadServerCertificate(string path, string password) => LoadPfx(path, password,
        OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable
            : X509KeyStorageFlags.EphemeralKeySet);

    private static void CreateLeaf(string issuerPath, string issuerPassword, string pfxPath, string pfxPassword, string subject)
    {
        using var issuer = LoadPfx(issuerPath, issuerPassword, X509KeyStorageFlags.Exportable);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={subject}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7f;
        using var certificate = request.Create(issuer, DateTimeOffset.UtcNow.AddMinutes(-1), issuer.NotAfter.ToUniversalTime().AddDays(-1), serial);
        using var withKey = certificate.CopyWithPrivateKey(key);
        File.WriteAllBytes(pfxPath, withKey.Export(X509ContentType.Pfx, pfxPassword));
    }

    private static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void ValidateLeaf(X509Certificate2 issuer, X509Certificate2 certificate)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(issuer);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        if (!chain.Build(certificate))
            throw new CryptographicException("A sandbox server certificate is not issued by its configured CA.");
    }

    private static X509Certificate2 LoadPfx(string path, string password, X509KeyStorageFlags flags) => new(path, password, flags);

    private static bool IsPathOrChild(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);

    private static void RejectReparsePoint(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("Sandbox state paths cannot be reparse points.");
    }

    private static void RestrictDirectory(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        var identities = new[]
        {
            WindowsIdentity.GetCurrent().User!,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
        }.Distinct().ToArray();
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var identity in identities)
            security.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }

    private sealed record SandboxSecrets(string CaPfxPassword, string PfxPassword, string JwtSecret, string AdminApiKey);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
