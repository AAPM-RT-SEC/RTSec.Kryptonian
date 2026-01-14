using AspNetCoreRateLimit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RTSec.Kryptonian.Api.Authentication;
using RTSec.Kryptonian.Api.Middleware;
using RTSec.Kryptonian.Application;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Infrastructure.Acme;
using RTSec.Kryptonian.Infrastructure.Crypto;
using RTSec.Kryptonian.Infrastructure.Data;
using RTSec.Kryptonian.Infrastructure.Security;
using Serilog;
using Serilog.Events;

// Configure Serilog early to catch startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting RTSec.Kryptonian API");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings
    // Note: CorrelationId and RequestId are pushed to LogContext by RequestIdMiddleware
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithClientIp()
        .Enrich.WithProperty("Application", "Kryptonian")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            "logs/kryptonian-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] [{RequestId}] {ClientIp} {Message:lj}{NewLine}{Exception}"));

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "RTSec.Kryptonian API", Version = "v1" });

        // Add API key authentication to Swagger
        c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "API Key authentication. Enter your API key in the header.",
            Name = "X-API-Key",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "ApiKey"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Add API key authentication (passes isDevelopment to allow dev bypass if configured)
    builder.Services.AddApiKeyAuthentication(builder.Configuration, builder.Environment.IsDevelopment());

    // Configure DbContext
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? builder.Configuration["Kryptonian:Database:ConnectionString"];

    if (string.IsNullOrEmpty(connectionString))
    {
        // Use in-memory database for development if no connection string provided
        builder.Services.AddDbContext<KryptonianDbContext>(options =>
            options.UseInMemoryDatabase("KryptonianDev"));
        Log.Warning("No connection string configured. Using in-memory database for development.");
    }
    else
    {
        // Configure Npgsql with dynamic JSON support for Dictionary<string, object> columns
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        builder.Services.AddDbContext<KryptonianDbContext>(options =>
            options.UseNpgsql(dataSource));
    }

    // Register Unit of Work (provides access to all repositories)
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

    // Register PKCS service
    builder.Services.AddSingleton<IPkcsService, PkcsService>();

    // Configure Data Protection (for encrypting ACME account keys in DB)
    builder.Services.AddDataProtection()
        .SetApplicationName("RTSec.Kryptonian");
    builder.Services.AddSingleton<IDataProtectionService, DataProtectionService>();

    // Register ACME challenge providers (singletons for token storage)
    builder.Services.AddSingleton<Http01ChallengeStore>();
    builder.Services.AddSingleton<Dns01ChallengeStub>();

    // Register background service for cleaning up expired HTTP-01 challenge tokens
    builder.Services.AddHostedService<Http01ChallengeCleanupService>();

    // Register CA connector factory (depends on above services)
    builder.Services.AddScoped<ICaConnectorFactory, CaConnectorFactory>();

    // Add application services (DTOs, mapping, business logic)
    builder.Services.AddApplicationServices();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<KryptonianDbContext>("database");

    // Add rate limiting for EST endpoints
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    builder.Services.AddInMemoryRateLimiting();

    var app = builder.Build();

    // Apply database migrations in development
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KryptonianDbContext>();

        // Only migrate if using a real database (not in-memory)
        if (!dbContext.Database.IsInMemory())
        {
            Log.Information("Applying database migrations...");
            dbContext.Database.Migrate();
            Log.Information("Database migrations applied successfully");
        }
    }

    // Configure the HTTP request pipeline
    // Request ID middleware must come first to establish correlation/request IDs for logging
    app.UseRequestIds(options =>
    {
        options.MinIdLength = 8;
        options.MaxIdLength = 128;
    });

    // Rate limiting for EST endpoints (must come before controller routing)
    app.UseIpRateLimiting();

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/api/status/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Partial class declaration for WebApplicationFactory in tests
public partial class Program { }
