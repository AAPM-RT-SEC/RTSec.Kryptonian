using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Web.Components;
using RTSec.Kryptonian.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "kryptonian.web.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// Configure HttpClient for API calls with validation
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
var apiUri = new Uri(apiBaseUrl);

// Enforce HTTPS in production to protect API keys
if (apiUri.Scheme != "https")
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "FATAL: ApiBaseUrl must use HTTPS in production to protect API keys. " +
            $"Current URL: {apiBaseUrl}. Update ApiBaseUrl to use https://");
    }
    if (!builder.Environment.IsDevelopment())
    {
        Console.WriteLine("WARNING: ApiBaseUrl is using HTTP. HTTPS is strongly recommended to protect API keys.");
    }
}

// Validate API key configuration - fail-fast in production
var apiKey = builder.Configuration["ApiKey"];
if (string.IsNullOrEmpty(apiKey))
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "FATAL: No API key configured in production. " +
            "Set 'ApiKey' in configuration or environment variables (e.g., ApiKey=your-key).");
    }
    Console.WriteLine("WARNING: No API key configured. Admin API calls will fail. " +
                      "Set 'ApiKey' in configuration or environment variables.");
}

void ConfigureApiClient(HttpClient client)
{
    client.BaseAddress = apiUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}

builder.Services.AddHttpClient("KryptonianApi", ConfigureApiClient);
builder.Services.AddHttpClient<IKryptonianApiClient, KryptonianApiClient>(ConfigureApiClient);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (IsPublicPath(context.Request.Path) || context.User.Identity?.IsAuthenticated == true)
    {
        await next();
        return;
    }

    var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
    context.Response.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
});
app.UseAntiforgery();

app.MapPost("/auth/login", async (
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<Program> logger) =>
    {
        var form = await context.Request.ReadFormAsync();
        var username = form["username"].ToString();
        var password = form["password"].ToString();
        var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return Results.Redirect($"/login?error={Uri.EscapeDataString("Username and password are required.")}&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var client = httpClientFactory.CreateClient("KryptonianApi");
        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(username, password));
            if (!response.IsSuccessStatusCode)
            {
                return Results.Redirect($"/login?error={Uri.EscapeDataString("Invalid username or password.")}&returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (auth == null)
            {
                return Results.Redirect($"/login?error={Uri.EscapeDataString("Login failed. Please try again.")}&returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, auth.UserId),
                new(ClaimTypes.Name, auth.Username),
                new(ClaimTypes.Role, auth.Role),
                new("role", auth.Role)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                ExpiresUtc = auth.ExpiresAt,
                IsPersistent = false
            });

            return Results.Redirect(returnUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Blazor web login failed");
            return Results.Redirect($"/login?error={Uri.EscapeDataString("Login failed. Please try again.")}&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }
    })
    .DisableAntiforgery();

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsPublicPath(PathString path)
{
    return path.StartsWithSegments("/login")
        || path.StartsWithSegments("/auth/login")
        || path.StartsWithSegments("/logout")
        || path.StartsWithSegments("/_blazor")
        || path.StartsWithSegments("/_framework")
        || path.StartsWithSegments("/_content")
        || path.StartsWithSegments("/img")
        || path == "/app.css"
        || path == "/favicon.png"
        || path == "/RTSec.Kryptonian.Web.styles.css";
}

static string NormalizeReturnUrl(string? returnUrl)
{
    return !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        ? returnUrl
        : "/";
}
