using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var gateway = ReadValue("Gateway URL", Environment.GetEnvironmentVariable("KRYPTONIAN_GATEWAY_URL") ?? "http://localhost:5000");
var displayName = ReadValue("Device display name", "Demo Activated Device");
var commonName = ReadValue("Device subject CN", $"demo-device-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
var serialNumber = ReadValue("Device serial number", $"SER-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
var manufacturer = ReadValue("Manufacturer", "Kryptonian Demo");
var model = ReadValue("Model", "Activated EST Demo");

if (!Uri.TryCreate(gateway.TrimEnd('/'), UriKind.Absolute, out var gatewayUri) ||
    (gatewayUri.Scheme != Uri.UriSchemeHttp && gatewayUri.Scheme != Uri.UriSchemeHttps))
{
    Console.Error.WriteLine($"Invalid gateway URL: {gateway}");
    Console.Error.WriteLine("Use an absolute http or https URL, for example http://localhost:5000.");
    return 2;
}

using var http = new HttpClient
{
    BaseAddress = gatewayUri
};

Console.WriteLine();
Console.WriteLine("1. Request pending device registration");
Console.WriteLine("2. Enroll with activation code");
Console.WriteLine("3. Do both");
var choice = ReadValue("Choose", "3");

if (choice is "1" or "3")
{
    await RequestPendingDeviceAsync(http, displayName, commonName, manufacturer, model, serialNumber);
}

if (choice is "2" or "3")
{
    Console.WriteLine();
    Console.WriteLine("Generate an activation code in the admin UI, then paste it here.");
    Console.WriteLine($"Device CN: {commonName}");
    Console.WriteLine($"Device serial: {serialNumber}");
    var activationCode = ReadValue("Activation code", null);
    await EnrollWithEstAsync(http, commonName, activationCode);
}

return 0;

static async Task RequestPendingDeviceAsync(
    HttpClient http,
    string displayName,
    string commonName,
    string manufacturer,
    string model,
    string serialNumber)
{
    var request = new DeviceApprovalRequest(
        displayName,
        commonName,
        manufacturer,
        model,
        serialNumber);

    using var response = await http.PostAsJsonAsync("/api/device-requests", request, Json.Options);
    var body = await response.Content.ReadAsStringAsync();

    Console.WriteLine();
    Console.WriteLine($"Pending request: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    Console.WriteLine(body);

    if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
    {
        response.EnsureSuccessStatusCode();
    }
}

static async Task EnrollWithEstAsync(HttpClient http, string commonName, string activationCode)
{
    var (csr, privateKeyPem) = CreateCsr(commonName);
    using var request = new HttpRequestMessage(HttpMethod.Post, "/.well-known/est/simpleenroll");
    request.Headers.Add("X-Activation-Code", activationCode);
    request.Headers.Add("Content-Transfer-Encoding", "base64");
    request.Content = new StringContent(Convert.ToBase64String(csr), Encoding.ASCII);
    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

    using var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();

    Console.WriteLine();
    Console.WriteLine($"EST enrollment: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine(body);
        return;
    }

    var outputPrefix = $"device-{commonName}";
    await File.WriteAllTextAsync($"{outputPrefix}.key.pem", privateKeyPem);
    await File.WriteAllTextAsync($"{outputPrefix}.pkcs7.b64", body);

    Console.WriteLine($"Certificate response saved to {outputPrefix}.pkcs7.b64");
    Console.WriteLine($"Private key saved to {outputPrefix}.key.pem");
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

static string ReadValue(string label, string? defaultValue)
{
    Console.Write(defaultValue == null ? $"{label}: " : $"{label} [{defaultValue}]: ");
    var value = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value.Trim();
    }

    if (defaultValue != null)
    {
        return defaultValue;
    }

    Console.WriteLine($"{label} is required.");
    return ReadValue(label, defaultValue);
}

internal sealed record DeviceApprovalRequest(
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("subjectCommonName")] string SubjectCommonName,
    [property: JsonPropertyName("manufacturer")] string Manufacturer,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("serialNumber")] string SerialNumber);

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
