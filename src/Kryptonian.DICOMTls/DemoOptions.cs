namespace Kryptonian.DICOMTls;

internal sealed class DemoOptions
{
    private const string DefaultGateway = "https://localhost:7443";

    public Uri? Gateway { get; private init; }

    /// <summary>Optional PEM CA used ONLY for gateway HTTPS trust in this demo.</summary>
    public string? GatewayCaPemPath { get; private init; }

    /// <summary>Admin API origin. Defaults to <see cref="Gateway"/>; set separately when the
    /// gateway serves EST and administration on different listeners.</summary>
    public Uri? AdminGateway { get; private init; }

    /// <summary>PEM CA trusting the admin listener; defaults to <see cref="GatewayCaPemPath"/>.</summary>
    public string? AdminCaPemPath { get; private init; }

    /// <summary>Skip the interactive cleanup prompt; devices stay registered unless an
    /// explicit archive/delete option was passed.</summary>
    public bool NonInteractive { get; private init; }

    public string? ActivationCodeA { get; private init; }

    public string? ActivationCodeB { get; private init; }

    public string? AdminApiKey { get; private init; }

    public int ActivationValidMinutes { get; private init; } = 60;

    public bool ArchiveCreatedDevices { get; private init; }

    public bool DeleteCreatedDevices { get; private init; }

    public string? CreateActivationDisplayName { get; private init; }

    public string? ArchiveDeviceId { get; private init; }

    public string? DeleteDeviceId { get; private init; }

    public string? DeviceACn { get; private init; }

    public string? DeviceBCn { get; private init; }

    public string? DeviceASerial { get; private init; }

    public string? DeviceBSerial { get; private init; }

    public string DeviceAAeTitle { get; private init; } = "KRYPTSCU";

    public string DeviceBAeTitle { get; private init; } = "KRYPTSCP";

    public int Port { get; private init; } = 11114;

    public int Count { get; private init; } = 3;

    public string OutputPath { get; private init; } = Path.Combine("artifacts", "dicom-tls-demo");

    public bool ShowHelp { get; private init; }

    public static DemoOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "--help" or "/?")
            {
                showHelp = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            string? value = null;
            var equals = key.IndexOf('=', StringComparison.Ordinal);
            if (equals >= 0)
            {
                value = key[(equals + 1)..];
                key = key[..equals];
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }

            values[key] = value;
        }

        var gateway = GetValue(values, "gateway")
            ?? Environment.GetEnvironmentVariable("KRYPTONIAN_GATEWAY_URL")
            ?? DefaultGateway;

        // Admin defaults to the EST origin, so existing single-listener usage is unchanged.
        var adminGateway = GetValue(values, "admin-gateway")
            ?? Environment.GetEnvironmentVariable("KRYPTONIAN_ADMIN_GATEWAY_URL")
            ?? gateway;

        return new DemoOptions
        {
            ShowHelp = showHelp,
            Gateway = Uri.TryCreate(gateway.TrimEnd('/'), UriKind.Absolute, out var gatewayUri) ? gatewayUri : null,
            GatewayCaPemPath = GetValue(values, "gateway-ca") ?? Environment.GetEnvironmentVariable("KRYPTONIAN_GATEWAY_CA_PEM"),
            AdminGateway = Uri.TryCreate(adminGateway.TrimEnd('/'), UriKind.Absolute, out var adminUri) ? adminUri : null,
            AdminCaPemPath = GetValue(values, "admin-ca") ?? Environment.GetEnvironmentVariable("KRYPTONIAN_ADMIN_CA_PEM"),
            NonInteractive = HasFlag(values, "non-interactive"),
            ActivationCodeA = GetValue(values, "activation-code-a") ?? Environment.GetEnvironmentVariable("KRYPTONIAN_ACTIVATION_CODE_A"),
            ActivationCodeB = GetValue(values, "activation-code-b") ?? Environment.GetEnvironmentVariable("KRYPTONIAN_ACTIVATION_CODE_B"),
            AdminApiKey = GetValue(values, "admin-api-key") ?? Environment.GetEnvironmentVariable("KRYPTONIAN_ADMIN_API_KEY"),
            ActivationValidMinutes = TryGetInt(values, "activation-valid-minutes", 60),
            ArchiveCreatedDevices = HasFlag(values, "archive-created-devices"),
            DeleteCreatedDevices = HasFlag(values, "delete-created-devices"),
            CreateActivationDisplayName = GetValue(values, "create-activation"),
            ArchiveDeviceId = GetValue(values, "archive-device"),
            DeleteDeviceId = GetValue(values, "delete-device"),
            DeviceACn = GetValue(values, "cn-a"),
            DeviceBCn = GetValue(values, "cn-b"),
            DeviceASerial = GetValue(values, "serial-a"),
            DeviceBSerial = GetValue(values, "serial-b"),
            DeviceAAeTitle = GetValue(values, "ae-a") ?? "KRYPTSCU",
            DeviceBAeTitle = GetValue(values, "ae-b") ?? "KRYPTSCP",
            Port = TryGetInt(values, "port", 11114),
            Count = TryGetInt(values, "count", 3),
            OutputPath = GetValue(values, "output") ?? Path.Combine("artifacts", "dicom-tls-demo")
        };
    }

    public bool IsValid(out string error)
    {
        if (Gateway == null || Gateway.Scheme != Uri.UriSchemeHttps)
        {
            error = "Gateway must be an absolute HTTPS URL.";
            return false;
        }

        if (AdminGateway == null || AdminGateway.Scheme != Uri.UriSchemeHttps)
        {
            error = "Admin gateway must be an absolute HTTPS URL.";
            return false;
        }

        if (ActivationValidMinutes is < 1 or > 1440)
        {
            error = "Activation code lifetime must be between 1 and 1440 minutes.";
            return false;
        }

        var managementCommandCount = new[] { CreateActivationDisplayName, ArchiveDeviceId, DeleteDeviceId }
            .Count(value => !string.IsNullOrWhiteSpace(value));
        if (managementCommandCount > 1)
        {
            error = "Use only one direct management command at a time.";
            return false;
        }

        if (HasManagementCommand)
        {
            error = string.Empty;
            return true;
        }

        if (Port is < 1 or > 65535)
        {
            error = "Port must be between 1 and 65535.";
            return false;
        }

        if (Count < 1)
        {
            error = "Count must be at least 1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool HasManagementCommand =>
        !string.IsNullOrWhiteSpace(CreateActivationDisplayName)
        || !string.IsNullOrWhiteSpace(ArchiveDeviceId)
        || !string.IsNullOrWhiteSpace(DeleteDeviceId);

    public static void PrintHelp()
    {
        Console.WriteLine("Kryptonian DICOM TLS demo");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/Kryptonian.DICOMTls");
        Console.WriteLine("  dotnet run --project src/Kryptonian.DICOMTls -- --activation-code-a <token> --activation-code-b <token> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --gateway <url>             Gateway base URL. Default: https://localhost:7443.");
        Console.WriteLine("  --gateway-ca <pem>          PEM CA used to trust the gateway HTTPS endpoint (EST + admin API).");
        Console.WriteLine("                              Revocation is NoCheck for this path; DICOM peer trust is unchanged.");
        Console.WriteLine("  --admin-gateway <url>         Admin API origin when separate from EST. Default: --gateway.");
        Console.WriteLine("  --admin-ca <pem>              PEM CA trusting the admin listener. Default: --gateway-ca.");
        Console.WriteLine("  --non-interactive           Leave created devices registered; no cleanup prompt.");
        Console.WriteLine("  --activation-code-a <token> Activation token for the sending device.");
        Console.WriteLine("  --activation-code-b <token> Activation token for the receiving device.");
        Console.WriteLine("  --admin-api-key <key>       Admin API key for activation creation and cleanup.");
        Console.WriteLine("  --activation-valid-minutes <n> Activation code lifetime. Default: 60.");
        Console.WriteLine("  --archive-created-devices   Archive devices auto-created by this run after the demo.");
        Console.WriteLine("  --delete-created-devices    Archive, then delete devices auto-created by this run after the demo.");
        Console.WriteLine("  --create-activation <name>  Create one device activation and exit.");
        Console.WriteLine("  --archive-device <id>       Archive/remove an existing device and exit.");
        Console.WriteLine("  --delete-device <id>        Permanently delete an archived device and exit.");
        Console.WriteLine("  --cn-a <cn>                 Sender certificate common name.");
        Console.WriteLine("  --cn-b <cn>                 Receiver certificate common name.");
        Console.WriteLine("  --serial-a <serial>         Sender serial number.");
        Console.WriteLine("  --serial-b <serial>         Receiver serial number.");
        Console.WriteLine("  --ae-a <ae>                 Sender AE title. Default: KRYPTSCU.");
        Console.WriteLine("  --ae-b <ae>                 Receiver AE title. Default: KRYPTSCP.");
        Console.WriteLine("  --port <port>               Receiver TLS Store SCP port. Default: 11114.");
        Console.WriteLine("  --count <n>                 Number of dummy DICOM files. Default: 3.");
        Console.WriteLine("  --output <path>             Artifact folder. Default: artifacts/dicom-tls-demo.");
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static bool HasFlag(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value)
            && (string.IsNullOrWhiteSpace(value) || bool.TryParse(value, out var parsed) && parsed);

    private static int TryGetInt(IReadOnlyDictionary<string, string?> values, string key, int fallback)
        => int.TryParse(GetValue(values, key), out var parsed) ? parsed : fallback;
}
