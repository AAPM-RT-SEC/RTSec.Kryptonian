using System.Net;
using System.Net.Http.Json;
using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Web.Services;

/// <summary>
/// HTTP client implementation for the Kryptonian Admin API.
/// </summary>
public class KryptonianApiClient : IKryptonianApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<KryptonianApiClient> _logger;

    public event EventHandler<ApiErrorEventArgs>? OnApiError;

    public KryptonianApiClient(HttpClient http, ILogger<KryptonianApiClient> logger, IConfiguration configuration)
    {
        _http = http;
        _logger = logger;

        var apiKey = configuration["ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    }

    #region CA Backends

    public async Task<ApiResult<IEnumerable<CaBackendDto>>> GetCaBackendsAsync(CancellationToken ct = default)
    {
        return await ExecuteAsync<IEnumerable<CaBackendDto>>(
            () => _http.GetFromJsonAsync<IEnumerable<CaBackendDto>>("/api/cas", ct),
            "Failed to get CA backends",
            ct);
    }

    public async Task<ApiResult<CaBackendDto>> GetCaBackendAsync(string id, CancellationToken ct = default)
    {
        return await ExecuteAsync<CaBackendDto>(
            () => _http.GetFromJsonAsync<CaBackendDto>($"/api/cas/{id}", ct),
            $"Failed to get CA backend {id}",
            ct);
    }

    public async Task<ApiResult<CaBackendDto>> CreateCaBackendAsync(CaBackendCreateDto dto, CancellationToken ct = default)
    {
        return await ExecuteWithResponseAsync<CaBackendDto>(
            () => _http.PostAsJsonAsync("/api/cas", dto, ct),
            "Failed to create CA backend",
            ct);
    }

    public async Task<ApiResult<CaBackendDto>> UpdateCaBackendAsync(string id, CaBackendUpdateDto dto, CancellationToken ct = default)
    {
        return await ExecuteWithResponseAsync<CaBackendDto>(
            () => _http.PutAsJsonAsync($"/api/cas/{id}", dto, ct),
            $"Failed to update CA backend {id}",
            ct);
    }

    public async Task<ApiResult<bool>> DeleteCaBackendAsync(string id, CancellationToken ct = default)
    {
        return await ExecuteDeleteAsync(
            () => _http.DeleteAsync($"/api/cas/{id}", ct),
            $"Failed to delete CA backend {id}",
            ct);
    }

    #endregion

    #region EST Profiles

    public async Task<ApiResult<IEnumerable<EstProfileDto>>> GetEstProfilesAsync(CancellationToken ct = default)
    {
        return await ExecuteAsync<IEnumerable<EstProfileDto>>(
            () => _http.GetFromJsonAsync<IEnumerable<EstProfileDto>>("/api/est-profiles", ct),
            "Failed to get EST profiles",
            ct);
    }

    public async Task<ApiResult<EstProfileDto>> GetEstProfileAsync(string id, CancellationToken ct = default)
    {
        return await ExecuteAsync<EstProfileDto>(
            () => _http.GetFromJsonAsync<EstProfileDto>($"/api/est-profiles/{id}", ct),
            $"Failed to get EST profile {id}",
            ct);
    }

    public async Task<ApiResult<EstProfileDto>> CreateEstProfileAsync(EstProfileCreateDto dto, CancellationToken ct = default)
    {
        return await ExecuteWithResponseAsync<EstProfileDto>(
            () => _http.PostAsJsonAsync("/api/est-profiles", dto, ct),
            "Failed to create EST profile",
            ct);
    }

    public async Task<ApiResult<EstProfileDto>> UpdateEstProfileAsync(string id, EstProfileUpdateDto dto, CancellationToken ct = default)
    {
        return await ExecuteWithResponseAsync<EstProfileDto>(
            () => _http.PutAsJsonAsync($"/api/est-profiles/{id}", dto, ct),
            $"Failed to update EST profile {id}",
            ct);
    }

    public async Task<ApiResult<bool>> DeleteEstProfileAsync(string id, CancellationToken ct = default)
    {
        return await ExecuteDeleteAsync(
            () => _http.DeleteAsync($"/api/est-profiles/{id}", ct),
            $"Failed to delete EST profile {id}",
            ct);
    }

    #endregion

    #region Enrollment Events

    public async Task<ApiResult<IEnumerable<EnrollmentEventDto>>> GetEnrollmentEventsAsync(string? profileId = null, int limit = 50, CancellationToken ct = default)
    {
        var url = $"/api/status/enrollments?limit={limit}";
        if (!string.IsNullOrEmpty(profileId))
        {
            url += $"&profileId={profileId}";
        }
        return await ExecuteAsync<IEnumerable<EnrollmentEventDto>>(
            () => _http.GetFromJsonAsync<IEnumerable<EnrollmentEventDto>>(url, ct),
            "Failed to get enrollment events",
            ct);
    }

    #endregion

    #region Dashboard

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var stats = new DashboardStats();

        // Fetch data in parallel (errors are handled per-call)
        var caBackendsTask = GetCaBackendsAsync(ct);
        var estProfilesTask = GetEstProfilesAsync(ct);
        var eventsTask = GetEnrollmentEventsAsync(limit: 100, ct: ct);

        await Task.WhenAll(caBackendsTask, estProfilesTask, eventsTask);

        var caBackendsResult = await caBackendsTask;
        var estProfilesResult = await estProfilesTask;
        var eventsResult = await eventsTask;

        if (caBackendsResult.Success && caBackendsResult.Data != null)
        {
            var caBackends = caBackendsResult.Data.ToList();
            stats.TotalCaBackends = caBackends.Count;
            stats.EnabledCaBackends = caBackends.Count(b => b.IsEnabled);
        }

        if (estProfilesResult.Success && estProfilesResult.Data != null)
        {
            var estProfiles = estProfilesResult.Data.ToList();
            stats.TotalEstProfiles = estProfiles.Count;
            stats.EnabledEstProfiles = estProfiles.Count(p => p.IsEnabled);
        }

        if (eventsResult.Success && eventsResult.Data != null)
        {
            var events = eventsResult.Data.ToList();

            // Calculate 24h statistics
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var recent24h = events.Where(e => e.Timestamp >= cutoff).ToList();
            stats.TotalEnrollments24h = recent24h.Count;
            stats.SuccessfulEnrollments24h = recent24h.Count(e => e.Status == "issued");
            stats.FailedEnrollments24h = recent24h.Count(e => e.Status == "error" || e.Status == "rejected");

            // Recent events for display
            stats.RecentEvents = events.Take(10);
        }

        return stats;
    }

    #endregion

    #region Private Helpers

    private async Task<ApiResult<T>> ExecuteAsync<T>(Func<Task<T?>> operation, string errorContext, CancellationToken ct)
    {
        try
        {
            var result = await operation();
            return result != null
                ? ApiResult<T>.Ok(result)
                : ApiResult<T>.Fail("No data returned", ApiErrorType.NotFound);
        }
        catch (HttpRequestException ex)
        {
            return HandleHttpException<T>(ex, errorContext);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return ApiResult<T>.Fail("Request was cancelled", ApiErrorType.Unknown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ErrorContext}", errorContext);
            RaiseError("An unexpected error occurred", ApiErrorType.Unknown);
            return ApiResult<T>.Fail(ex.Message, ApiErrorType.Unknown);
        }
    }

    private async Task<ApiResult<T>> ExecuteWithResponseAsync<T>(Func<Task<HttpResponseMessage>> operation, string errorContext, CancellationToken ct)
    {
        try
        {
            var response = await operation();

            if (!response.IsSuccessStatusCode)
            {
                return HandleErrorResponse<T>(response, errorContext);
            }

            var result = await response.Content.ReadFromJsonAsync<T>(ct);
            return result != null
                ? ApiResult<T>.Ok(result)
                : ApiResult<T>.Fail("No data returned", ApiErrorType.Unknown);
        }
        catch (HttpRequestException ex)
        {
            return HandleHttpException<T>(ex, errorContext);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return ApiResult<T>.Fail("Request was cancelled", ApiErrorType.Unknown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ErrorContext}", errorContext);
            RaiseError("An unexpected error occurred", ApiErrorType.Unknown);
            return ApiResult<T>.Fail(ex.Message, ApiErrorType.Unknown);
        }
    }

    private async Task<ApiResult<bool>> ExecuteDeleteAsync(Func<Task<HttpResponseMessage>> operation, string errorContext, CancellationToken ct)
    {
        try
        {
            var response = await operation();

            if (!response.IsSuccessStatusCode)
            {
                HandleErrorResponse<bool>(response, errorContext);
                return ApiResult<bool>.Fail(GetErrorMessage(response.StatusCode), GetErrorType(response.StatusCode));
            }

            return ApiResult<bool>.Ok(true);
        }
        catch (HttpRequestException ex)
        {
            return HandleHttpException<bool>(ex, errorContext);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return ApiResult<bool>.Fail("Request was cancelled", ApiErrorType.Unknown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ErrorContext}", errorContext);
            RaiseError("An unexpected error occurred", ApiErrorType.Unknown);
            return ApiResult<bool>.Fail(ex.Message, ApiErrorType.Unknown);
        }
    }

    private ApiResult<T> HandleHttpException<T>(HttpRequestException ex, string errorContext)
    {
        _logger.LogError(ex, "{ErrorContext}", errorContext);

        var statusCode = ex.StatusCode;
        var errorType = statusCode.HasValue ? GetErrorType(statusCode.Value) : ApiErrorType.NetworkError;
        var message = statusCode.HasValue ? GetErrorMessage(statusCode.Value) : "Network error - unable to connect to API";

        RaiseError(message, errorType, (int?)statusCode);
        return ApiResult<T>.Fail(message, errorType);
    }

    private ApiResult<T> HandleErrorResponse<T>(HttpResponseMessage response, string errorContext)
    {
        var statusCode = response.StatusCode;
        var errorType = GetErrorType(statusCode);
        var message = GetErrorMessage(statusCode);

        _logger.LogWarning("{ErrorContext}: {StatusCode} - {Message}", errorContext, (int)statusCode, message);
        RaiseError(message, errorType, (int)statusCode);

        return ApiResult<T>.Fail(message, errorType);
    }

    private static ApiErrorType GetErrorType(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => ApiErrorType.Unauthorized,
        HttpStatusCode.Forbidden => ApiErrorType.Forbidden,
        HttpStatusCode.NotFound => ApiErrorType.NotFound,
        HttpStatusCode.BadRequest => ApiErrorType.ValidationError,
        >= HttpStatusCode.InternalServerError => ApiErrorType.ServerError,
        _ => ApiErrorType.Unknown
    };

    private static string GetErrorMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Authentication failed. Please check your API key configuration.",
        HttpStatusCode.Forbidden => "Access denied. You don't have permission for this operation.",
        HttpStatusCode.NotFound => "The requested resource was not found.",
        HttpStatusCode.BadRequest => "Invalid request. Please check the input data.",
        >= HttpStatusCode.InternalServerError => "Server error. Please try again later or contact support.",
        _ => $"Request failed with status {(int)statusCode}"
    };

    private void RaiseError(string message, ApiErrorType errorType, int? statusCode = null)
    {
        OnApiError?.Invoke(this, new ApiErrorEventArgs
        {
            Message = message,
            ErrorType = errorType,
            StatusCode = statusCode
        });
    }

    #endregion
}
