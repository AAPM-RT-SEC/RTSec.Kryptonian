using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace RTSec.Kryptonian.Api.Authentication;

/// <summary>
/// Options for API key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "X-API-Key";

    /// <summary>
    /// Valid API keys (comma-separated in configuration).
    /// </summary>
    public HashSet<string> ApiKeys { get; set; } = new();

    /// <summary>
    /// If true, allows running without API keys in development mode.
    /// Default is false - API keys are required.
    /// </summary>
    public bool AllowAnonymousInDevelopment { get; set; } = false;
}

/// <summary>
/// Authentication handler for API key-based authentication.
/// Keys are read from environment variables or configuration.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Allow anonymous access in development mode if configured
        if (Options.AllowAnonymousInDevelopment && Options.ApiKeys.Count == 0)
        {
            Logger.LogDebug("Bypassing API key authentication in development mode");
            var devClaims = new[]
            {
                new Claim(ClaimTypes.Name, "DevelopmentUser"),
                new Claim(ClaimTypes.AuthenticationMethod, "DevBypass")
            };
            var devIdentity = new ClaimsIdentity(devClaims, Scheme.Name);
            var devPrincipal = new ClaimsPrincipal(devIdentity);
            var devTicket = new AuthenticationTicket(devPrincipal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(devTicket));
        }

        // Check if API key header is present
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key header is missing"));
        }

        var apiKey = apiKeyHeader.ToString();

        if (string.IsNullOrEmpty(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key is empty"));
        }

        // Validate API key
        if (!Options.ApiKeys.Contains(apiKey))
        {
            Logger.LogWarning("Invalid API key attempted: {KeyPrefix}...", apiKey.Length > 8 ? apiKey[..8] : apiKey);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        // Create claims identity for authenticated request
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ApiKeyUser"),
            new Claim(ClaimTypes.AuthenticationMethod, "ApiKey")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"ApiKey realm=\"Kryptonian Admin API\"";
        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for configuring API key authentication.
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    /// <summary>
    /// Adds API key authentication to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="isDevelopment">Whether the application is running in development mode.</param>
    /// <returns>The authentication builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no API keys are configured and not in development mode with bypass enabled.</exception>
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        // Load API keys from configuration/environment
        var apiKeysConfig = configuration["Kryptonian:AdminApi:ApiKeys"]
            ?? Environment.GetEnvironmentVariable("KRYPTONIAN__ADMINAPI__APIKEYS");

        var allowAnonymousDev = configuration.GetValue<bool>("Kryptonian:AdminApi:AllowAnonymousInDevelopment", false);

        var apiKeys = new HashSet<string>();
        if (!string.IsNullOrEmpty(apiKeysConfig))
        {
            var keys = apiKeysConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var key in keys)
            {
                apiKeys.Add(key);
            }
        }

        // Fail fast if no API keys are configured (unless in dev mode with bypass)
        if (apiKeys.Count == 0)
        {
            if (isDevelopment && allowAnonymousDev)
            {
                // Log warning but allow startup in dev mode
                Console.WriteLine("WARNING: No API keys configured. Admin API authentication is bypassed in development mode.");
            }
            else
            {
                throw new InvalidOperationException(
                    "No API keys configured. Set KRYPTONIAN__ADMINAPI__APIKEYS environment variable or " +
                    "Kryptonian:AdminApi:ApiKeys in configuration. " +
                    "For development, set Kryptonian:AdminApi:AllowAnonymousInDevelopment=true to bypass.");
            }
        }

        return services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.DefaultScheme,
                options =>
                {
                    options.ApiKeys = apiKeys;
                    options.AllowAnonymousInDevelopment = allowAnonymousDev && isDevelopment;
                });
    }
}
