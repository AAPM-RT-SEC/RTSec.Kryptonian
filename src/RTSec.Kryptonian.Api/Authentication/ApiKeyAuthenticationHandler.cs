using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "X-API-Key";

    public HashSet<string> ApiKeys { get; set; } = new();
    public bool AllowAnonymousInDevelopment { get; set; } = false;
}

/// <summary>
/// Validates X-API-Key requests. Checks config-based static keys (SystemAdmin)
/// then DB-stored user keys (role from record). Returns NoResult when header absent,
/// allowing JWT Bearer to handle Authorization header requests.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly KryptonianDbContext _dbContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        KryptonianDbContext dbContext)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Dev bypass — only when no static keys configured and dev mode enabled
        if (Options.AllowAnonymousInDevelopment && Options.ApiKeys.Count == 0)
        {
            Logger.LogDebug("Bypassing API key authentication in development mode");
            return AuthenticateResult.Success(BuildTicket("DevelopmentUser", null, "SystemAdmin", "DevBypass"));
        }

        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeader))
            return AuthenticateResult.NoResult();

        var rawKey = apiKeyHeader.ToString();
        if (string.IsNullOrEmpty(rawKey))
            return AuthenticateResult.Fail("API key is empty");

        // Config-based static keys → SystemAdmin (backward compat)
        if (Options.ApiKeys.Contains(rawKey))
            return AuthenticateResult.Success(BuildTicket("ApiKeyUser", null, "SystemAdmin", "ApiKey"));

        // DB-stored user API keys (prefixed kry_)
        if (rawKey.StartsWith("kry_", StringComparison.Ordinal))
        {
            var keyHash = ComputeSha256(rawKey);
            var apiKey = await _dbContext.ApiKeys
                .Include(k => k.User)
                .FirstOrDefaultAsync(k =>
                    k.KeyHash == keyHash &&
                    k.RevokedAt == null &&
                    (k.ExpiresAt == null || k.ExpiresAt > DateTimeOffset.UtcNow),
                    Context.RequestAborted);

            if (apiKey != null && apiKey.User?.IsActive == true)
            {
                // Best-effort update of LastUsedAt — don't fail the request if it errors
                try
                {
                    apiKey.LastUsedAt = DateTimeOffset.UtcNow;
                    await _dbContext.SaveChangesAsync(Context.RequestAborted);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to update LastUsedAt for API key {Prefix}", apiKey.Prefix);
                }

                return AuthenticateResult.Success(
                    BuildTicket(apiKey.User.Username, apiKey.UserId.ToString(), apiKey.Role.ToString(), "ApiKey"));
            }
        }

        Logger.LogWarning("Invalid API key attempted: {KeyPrefix}...",
            rawKey.Length > 8 ? rawKey[..8] : rawKey);
        return AuthenticateResult.Fail("Invalid API key");
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "ApiKey realm=\"Kryptonian Admin API\"";
        return Task.CompletedTask;
    }

    private AuthenticationTicket BuildTicket(string name, string? userId, string role, string method)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(ClaimTypes.AuthenticationMethod, method),
            new("role", role),
        };
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

        return new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)),
            Scheme.Name);
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        var apiKeysConfig = configuration["Kryptonian:AdminApi:ApiKeys"]
            ?? Environment.GetEnvironmentVariable("KRYPTONIAN__ADMINAPI__APIKEYS");
        var allowAnonymousDev = bool.TryParse(
            configuration["Kryptonian:AdminApi:AllowAnonymousInDevelopment"], out var v) && v;

        var apiKeys = new HashSet<string>();
        if (!string.IsNullOrEmpty(apiKeysConfig))
        {
            foreach (var key in apiKeysConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                apiKeys.Add(key);
        }

        if (apiKeys.Count == 0)
        {
            Console.WriteLine("INFO: No static API keys configured (KRYPTONIAN__ADMINAPI__APIKEYS). " +
                "Users will authenticate via JWT or DB-stored API keys.");
        }

        builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            options =>
            {
                options.ApiKeys = apiKeys;
                options.AllowAnonymousInDevelopment = allowAnonymousDev && isDevelopment;
            });

        return builder;
    }
}
