using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Web.Services;

/// <summary>
/// Client interface for the Kryptonian Admin API.
/// </summary>
public interface IKryptonianApiClient
{
    /// <summary>
    /// Event raised when an API error occurs, allowing UI to display notifications.
    /// </summary>
    event EventHandler<ApiErrorEventArgs>? OnApiError;

    // CA Backends
    Task<ApiResult<IEnumerable<CaBackendDto>>> GetCaBackendsAsync(CancellationToken ct = default);
    Task<ApiResult<CaBackendDto>> GetCaBackendAsync(string id, CancellationToken ct = default);
    Task<ApiResult<CaBackendDto>> CreateCaBackendAsync(CaBackendCreateDto dto, CancellationToken ct = default);
    Task<ApiResult<CaBackendDto>> UpdateCaBackendAsync(string id, CaBackendUpdateDto dto, CancellationToken ct = default);
    Task<ApiResult<bool>> DeleteCaBackendAsync(string id, CancellationToken ct = default);
    Task<ApiResult<CaBackendDto>> ActivateCaBackendAsync(string id, CancellationToken ct = default);

    // Devices
    Task<ApiResult<IEnumerable<DeviceDto>>> GetDevicesAsync(CancellationToken ct = default);
    Task<ApiResult<DeviceDto>> CreateDeviceAsync(DeviceCreateDto dto, CancellationToken ct = default);
    Task<ApiResult<DeviceActivationCodeDto>> GenerateActivationCodeAsync(string id, int? validForMinutes = null, CancellationToken ct = default);
    Task<ApiResult<DeviceDto>> ApproveDeviceAsync(string id, CancellationToken ct = default);
    Task<ApiResult<DeviceDto>> RemoveDeviceAsync(string id, CancellationToken ct = default);
    Task<ApiResult<IEnumerable<CertificateDto>>> GetDeviceCertificatesAsync(string id, CancellationToken ct = default);
    Task<ApiResult<DemoEnrollResponseDto>> DemoEnrollDeviceAsync(string id, CancellationToken ct = default);

    // EST Profiles
    Task<ApiResult<IEnumerable<EstProfileDto>>> GetEstProfilesAsync(CancellationToken ct = default);
    Task<ApiResult<EstProfileDto>> GetEstProfileAsync(string id, CancellationToken ct = default);
    Task<ApiResult<EstProfileDto>> CreateEstProfileAsync(EstProfileCreateDto dto, CancellationToken ct = default);
    Task<ApiResult<EstProfileDto>> UpdateEstProfileAsync(string id, EstProfileUpdateDto dto, CancellationToken ct = default);
    Task<ApiResult<bool>> DeleteEstProfileAsync(string id, CancellationToken ct = default);

    // Enrollment Events
    Task<ApiResult<IEnumerable<EnrollmentEventDto>>> GetEnrollmentEventsAsync(string? profileId = null, int limit = 50, CancellationToken ct = default);

    // Dashboard Statistics
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Result wrapper for API calls with error information.
/// </summary>
public class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public ApiErrorType ErrorType { get; init; }

    public static ApiResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResult<T> Fail(string message, ApiErrorType errorType = ApiErrorType.Unknown) =>
        new() { Success = false, ErrorMessage = message, ErrorType = errorType };
}

/// <summary>
/// Types of API errors for appropriate UI handling.
/// </summary>
public enum ApiErrorType
{
    Unknown,
    NetworkError,
    Unauthorized,      // 401 - API key missing/invalid
    Forbidden,         // 403 - Insufficient permissions
    NotFound,          // 404
    ValidationError,   // 400
    ServerError        // 5xx
}

/// <summary>
/// Event args for API error events.
/// </summary>
public class ApiErrorEventArgs : EventArgs
{
    public required string Message { get; init; }
    public required ApiErrorType ErrorType { get; init; }
    public int? StatusCode { get; init; }
}

/// <summary>
/// Dashboard statistics aggregation.
/// </summary>
public class DashboardStats
{
    public int TotalCaBackends { get; set; }
    public int EnabledCaBackends { get; set; }
    public int TotalEstProfiles { get; set; }
    public int EnabledEstProfiles { get; set; }
    public int TotalEnrollments24h { get; set; }
    public int SuccessfulEnrollments24h { get; set; }
    public int FailedEnrollments24h { get; set; }
    public IEnumerable<EnrollmentEventDto> RecentEvents { get; set; } = [];
}
