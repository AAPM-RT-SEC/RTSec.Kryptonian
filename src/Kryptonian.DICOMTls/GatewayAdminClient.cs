using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kryptonian.DICOMTls;

internal sealed class GatewayAdminClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public GatewayAdminClient(Uri gateway, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _http = new HttpClient { BaseAddress = gateway };
        _http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }

    public async Task<ProvisionedActivation> CreateActivationAsync(
        string displayName,
        int validForMinutes,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var device = await PostAsync<AdminDeviceDto>(
            "/api/devices",
            new DeviceCreateRequest(displayName),
            ct);

        var activation = await PostAsync<DeviceActivationCodeDto>(
            $"/api/devices/{Uri.EscapeDataString(device.Id)}/activation-code",
            new ActivationCodeCreateRequest(validForMinutes),
            ct);

        return new ProvisionedActivation(device.Id, device.DisplayName, activation.ActivationCode, activation.ExpiresAt);
    }

    public Task<AdminDeviceDto> ArchiveDeviceAsync(string deviceId, CancellationToken ct = default)
        => PostAsync<AdminDeviceDto>(
            $"/api/devices/{Uri.EscapeDataString(deviceId)}/remove",
            body: null,
            ct);

    public async Task DeleteDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        using var response = await _http.DeleteAsync($"/api/devices/{Uri.EscapeDataString(deviceId)}", ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowAdminApiExceptionAsync(response, ct);
        }
    }

    public void Dispose()
        => _http.Dispose();

    private async Task<T> PostAsync<T>(string path, object? body, CancellationToken ct)
    {
        using var response = body == null
            ? await _http.PostAsync(path, content: null, ct)
            : await _http.PostAsJsonAsync(path, body, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowAdminApiExceptionAsync(response, ct);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException($"Gateway admin API returned an empty response for {path}.");
    }

    private static async Task ThrowAdminApiExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var message = TryReadGatewayError(body)
            ?? response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Unauthorized admin API request. Check --admin-api-key or KRYPTONIAN_ADMIN_API_KEY.",
                HttpStatusCode.NotFound => "The target device was not found.",
                HttpStatusCode.BadRequest => "The gateway rejected the admin API request.",
                _ => $"Gateway admin API request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}."
            };

        if (response.StatusCode == HttpStatusCode.BadRequest && message.Contains("Only archived devices", StringComparison.OrdinalIgnoreCase))
        {
            message += " Archive the device first with --archive-device.";
        }

        throw new InvalidOperationException($"{message}{(string.IsNullOrWhiteSpace(body) ? string.Empty : $" Body: {body}")}");
    }

    private static string? TryReadGatewayError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                ? error.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record DeviceCreateRequest(
        [property: JsonPropertyName("displayName")] string DisplayName);

    private sealed record ActivationCodeCreateRequest(
        [property: JsonPropertyName("validForMinutes")] int ValidForMinutes);
}

internal sealed record ProvisionedActivation(
    string DeviceId,
    string DisplayName,
    string ActivationCode,
    DateTimeOffset ExpiresAt);

internal sealed record AdminDeviceDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("status")] string Status);

internal sealed record DeviceActivationCodeDto(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("activationCode")] string ActivationCode,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);
