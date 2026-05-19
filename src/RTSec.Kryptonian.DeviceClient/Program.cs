using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    var request = new DeviceApprovalRequest(
        options.DisplayName,
        options.SubjectCommonName,
        options.Manufacturer,
        options.Model,
        options.SerialNumber);

    Console.WriteLine($"Requesting gateway approval from {new Uri(options.GatewayUrl!, "/api/device-requests")}");
    Console.WriteLine($"Device CN: {request.SubjectCommonName}");
    Console.WriteLine();

    using var response = await http.PostAsJsonAsync("/api/device-requests", request, Json.Options);
    var responseText = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Approval request failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        Console.Error.WriteLine(responseText);
        return 1;
    }

    var approval = JsonSerializer.Deserialize<DeviceApprovalResponse>(responseText, Json.Options);
    if (approval == null)
    {
        Console.WriteLine(responseText);
        return 0;
    }

    Console.WriteLine("Approval request accepted.");
    Console.WriteLine($"Device ID: {approval.DeviceId}");
    Console.WriteLine($"Status: {approval.Status}");
    Console.WriteLine($"Subject CN: {approval.SubjectCommonName}");
    Console.WriteLine(approval.Message);
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

internal sealed record DeviceApprovalRequest(
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("subjectCommonName")] string SubjectCommonName,
    [property: JsonPropertyName("manufacturer")] string? Manufacturer,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("serialNumber")] string? SerialNumber);

internal sealed record DeviceApprovalResponse(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("subjectCommonName")] string SubjectCommonName,
    [property: JsonPropertyName("message")] string Message);

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}

internal sealed class DeviceClientOptions
{
    public Uri? GatewayUrl { get; private init; }

    public string DisplayName { get; private init; } = string.Empty;

    public string SubjectCommonName { get; private init; } = string.Empty;

    public string? Manufacturer { get; private init; }

    public string? Model { get; private init; }

    public string? SerialNumber { get; private init; }

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
            ?? "http://localhost:5000";
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
            SerialNumber = serial
        };
    }

    public bool IsValid(out string error)
    {
        if (GatewayUrl == null)
        {
            error = "Gateway URL must be an absolute URL.";
            return false;
        }

        if (GatewayUrl.Scheme != Uri.UriSchemeHttp && GatewayUrl.Scheme != Uri.UriSchemeHttps)
        {
            error = "Gateway URL must use http or https.";
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

        error = string.Empty;
        return true;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Kryptonian medical device approval requester");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/RTSec.Kryptonian.DeviceClient -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --gateway <url>        Gateway base URL. Defaults to KRYPTONIAN_GATEWAY_URL or http://localhost:5000.");
        Console.WriteLine("  --name <name>          Device display name.");
        Console.WriteLine("  --cn <common-name>     CSR subject common name the gateway should approve.");
        Console.WriteLine("  --manufacturer <name>  Device manufacturer.");
        Console.WriteLine("  --model <name>         Device model.");
        Console.WriteLine("  --serial <value>       Device serial number.");
        Console.WriteLine("  --help                 Show help.");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  dotnet run --project src/RTSec.Kryptonian.DeviceClient -- --gateway http://localhost:5000 --name \"Scanner 7\" --cn scanner-7 --serial SCAN-7");
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}
