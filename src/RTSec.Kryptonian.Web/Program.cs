using MudBlazor.Services;
using RTSec.Kryptonian.Web.Components;
using RTSec.Kryptonian.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

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

builder.Services.AddHttpClient<IKryptonianApiClient, KryptonianApiClient>(client =>
{
    client.BaseAddress = apiUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
