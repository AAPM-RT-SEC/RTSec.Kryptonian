using System.Text.RegularExpressions;

namespace RTSec.Kryptonian.Api.Middleware;

/// <summary>
/// Middleware that manages correlation IDs and internal request IDs.
/// - Validates and accepts X-Correlation-ID from upstream if valid
/// - Always generates an internal X-Request-ID for untrusted upstream scenarios
/// - Adds both IDs to response headers and logging context
/// </summary>
public partial class RequestIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestIdMiddleware> _logger;
    private readonly RequestIdOptions _options;

    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const string RequestIdHeader = "X-Request-ID";
    public const string CorrelationIdLogProperty = "CorrelationId";
    public const string RequestIdLogProperty = "RequestId";

    public RequestIdMiddleware(
        RequestDelegate next,
        ILogger<RequestIdMiddleware> logger,
        RequestIdOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new RequestIdOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        var requestId = GenerateRequestId();

        // Store in HttpContext for downstream use
        context.Items[CorrelationIdLogProperty] = correlationId;
        context.Items[RequestIdLogProperty] = requestId;

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            context.Response.Headers[RequestIdHeader] = requestId;
            return Task.CompletedTask;
        });

        // Add to logging context using Serilog's LogContext
        using (Serilog.Context.LogContext.PushProperty(CorrelationIdLogProperty, correlationId))
        using (Serilog.Context.LogContext.PushProperty(RequestIdLogProperty, requestId))
        {
            await _next(context);
        }
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // Try to get correlation ID from request header
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue))
        {
            var providedId = headerValue.ToString();

            if (IsValidCorrelationId(providedId))
            {
                _logger.LogDebug("Using provided correlation ID: {CorrelationId}", providedId);
                return providedId;
            }
            else
            {
                _logger.LogWarning(
                    "Invalid correlation ID provided: {ProvidedId} (length: {Length}). Generating new ID.",
                    providedId.Length > 100 ? providedId[..100] + "..." : providedId,
                    providedId.Length);
            }
        }

        // Generate a new correlation ID
        var newId = GenerateCorrelationId();
        _logger.LogDebug("Generated new correlation ID: {CorrelationId}", newId);
        return newId;
    }

    private bool IsValidCorrelationId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        // Check length constraints
        if (id.Length < _options.MinIdLength || id.Length > _options.MaxIdLength)
            return false;

        // Check character set (alphanumeric, hyphens, underscores only)
        return ValidIdPattern().IsMatch(id);
    }

    private static string GenerateCorrelationId()
    {
        // Use a format that's URL-safe and reasonably short but unique
        return Guid.NewGuid().ToString("N")[..16] + "-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x");
    }

    private static string GenerateRequestId()
    {
        // Internal request ID is always server-generated for traceability
        return Guid.NewGuid().ToString("N");
    }

    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$")]
    private static partial Regex ValidIdPattern();
}

/// <summary>
/// Options for request ID middleware configuration.
/// </summary>
public class RequestIdOptions
{
    /// <summary>
    /// Minimum length for an accepted correlation ID. Default: 8.
    /// </summary>
    public int MinIdLength { get; set; } = 8;

    /// <summary>
    /// Maximum length for an accepted correlation ID. Default: 128.
    /// </summary>
    public int MaxIdLength { get; set; } = 128;
}

/// <summary>
/// Extension methods for RequestIdMiddleware.
/// </summary>
public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestIds(this IApplicationBuilder builder, Action<RequestIdOptions>? configure = null)
    {
        var options = new RequestIdOptions();
        configure?.Invoke(options);
        return builder.UseMiddleware<RequestIdMiddleware>(options);
    }

    /// <summary>
    /// Gets the correlation ID from the current HTTP context.
    /// </summary>
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items.TryGetValue(RequestIdMiddleware.CorrelationIdLogProperty, out var value)
            ? value as string
            : null;
    }

    /// <summary>
    /// Gets the internal request ID from the current HTTP context.
    /// </summary>
    public static string? GetRequestId(this HttpContext context)
    {
        return context.Items.TryGetValue(RequestIdMiddleware.RequestIdLogProperty, out var value)
            ? value as string
            : null;
    }
}
