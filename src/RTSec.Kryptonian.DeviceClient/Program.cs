using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var options = DeviceClientOptions.Parse(args);
if (options.ShowHelp)
{
    DeviceClientOptions.PrintHelp();
    return 0;
}

if (!options.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    Console.Error.WriteLine();
    DeviceClientOptions.PrintHelp();
    return 2;
}

using var http = new HttpClient
{
    BaseAddress = options.GatewayUrl
};

try
{
    Console.WriteLine($"Activating device through {new Uri(options.GatewayUrl!, "/.well-known/est/simpleenroll")}");
    Console.WriteLine($"Device alias: {options.DisplayName}");
    Console.WriteLine($"Device CN: {options.SubjectCommonName}");
    Console.WriteLine($"Serial: {options.SerialNumber}");
    Console.WriteLine();

    var (csr, privateKeyPem) = CreateCsr(options.SubjectCommonName);
    using var request = new HttpRequestMessage(HttpMethod.Post, "/.well-known/est/simpleenroll");
    request.Headers.Add("X-Activation-Code", options.ActivationCode);
    request.Headers.Add("X-Device-Manufacturer", options.Manufacturer);
    request.Headers.Add("X-Device-Model", options.Model);
    request.Headers.Add("X-Device-Serial-Number", options.SerialNumber);
    request.Headers.Add("Content-Transfer-Encoding", "base64");
    request.Content = new StringContent(Convert.ToBase64String(csr), Encoding.ASCII);
    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

    using var response = await http.SendAsync(request);
    var responseText = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Activation enrollment failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        Console.Error.WriteLine(responseText);
        return 1;
    }

    var outputPrefix = $"device-{options.SubjectCommonName}";
    await File.WriteAllTextAsync($"{outputPrefix}.key.pem", privateKeyPem);
    await File.WriteAllTextAsync($"{outputPrefix}.pkcs7.b64", responseText);

    Console.WriteLine("Activation certificate issued.");
    Console.WriteLine($"Certificate response saved to {outputPrefix}.pkcs7.b64");
    Console.WriteLine($"Private key saved to {outputPrefix}.key.pem");
    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Unable to reach the gateway: {ex.Message}");
    return 1;
}
catch (TaskCanceledException ex)
{
    Console.Error.WriteLine($"Gateway request timed out or was cancelled: {ex.Message}");
    return 1;
}

static (byte[] Csr, string PrivateKeyPem) CreateCsr(string commonName)
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(
        new X500DistinguishedName($"CN={commonName}"),
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    return (request.CreateSigningRequest(), rsa.ExportPkcs8PrivateKeyPem());
}

internal sealed class DeviceClientOptions
{
    public Uri? GatewayUrl { get; private init; }

    public string DisplayName { get; private init; } = string.Empty;

    public string SubjectCommonName { get; private init; } = string.Empty;

    public string? Manufacturer { get; private init; }

    public string? Model { get; private init; }

    public string? SerialNumber { get; private init; }

    public string ActivationCode { get; private init; } = string.Empty;

    public bool ShowHelp { get; private init; }

    public static DeviceClientOptions Parse(string[] args)
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
            var equalsIndex = key.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex >= 0)
            {
                value = key[(equalsIndex + 1)..];
                key = key[..equalsIndex];
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }

            values[key] = value;
        }

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var gateway = GetValue(values, "gateway")
            ?? Environment.GetEnvironmentVariable("KRYPTONIAN_GATEWAY_URL")
            ?? "https://localhost:8443";
        var serial = GetValue(values, "serial") ?? $"DEV-{stamp}";
        var cn = GetValue(values, "cn") ?? $"medical-device-{serial}".ToLowerInvariant();

        return new DeviceClientOptions
        {
            ShowHelp = showHelp,
            GatewayUrl = Uri.TryCreate(gateway.TrimEnd('/'), UriKind.Absolute, out var gatewayUri) ? gatewayUri : null,
            DisplayName = GetValue(values, "name") ?? $"Medical Device {serial}",
            SubjectCommonName = cn,
            Manufacturer = GetValue(values, "manufacturer") ?? "Kryptonian Demo",
            Model = GetValue(values, "model") ?? "MEDIATE Device",
            SerialNumber = serial,
            ActivationCode = GetValue(values, "activation-code") ?? Environment.GetEnvironmentVariable("KRYPTONIAN_ACTIVATION_CODE") ?? string.Empty
        };
    }

    public bool IsValid(out string error)
    {
        if (GatewayUrl == null)
        {
            error = "Gateway URL must be an absolute URL.";
            return false;
        }

        if (GatewayUrl.Scheme != Uri.UriSchemeHttps)
        {
            error = "Gateway URL must use https for EST activation.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            error = "Device display name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SubjectCommonName))
        {
            error = "Device subject CN is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SerialNumber))
        {
            error = "Device serial number is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ActivationCode))
        {
            error = "Activation code is required. Use --activation-code or KRYPTONIAN_ACTIVATION_CODE.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Kryptonian medical device activation client");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/RTSec.Kryptonian.DeviceClient -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --gateway <url>        Gateway base URL. Defaults to KRYPTONIAN_GATEWAY_URL or https://localhost:8443.");
        Console.WriteLine("  --name <name>          Device display name.");
        Console.WriteLine("  --cn <common-name>     CSR subject common name the gateway should approve.");
        Console.WriteLine("  --manufacturer <name>  Device manufacturer.");
        Console.WriteLine("  --model <name>         Device model.");
        Console.WriteLine("  --serial <value>       Device serial number.");
        Console.WriteLine("  --activation-code <c>  One-time code generated from the Devices tab.");
        Console.WriteLine("  --help                 Show help.");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  dotnet run --project src/RTSec.Kryptonian.DeviceClient -- --gateway https://localhost:8443 --name \"Scanner 7\" --cn scanner-7 --serial SCAN-7 --activation-code ABCD-EFGH");
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}
