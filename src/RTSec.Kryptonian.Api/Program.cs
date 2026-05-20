using AspNetCoreRateLimit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
    builder.Services.AddHttpClient();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("KryptonianUi", policy =>
        {
            var origins = builder.Configuration
                .GetSection("Kryptonian:Ui:AllowedOrigins")
                .Get<string[]>()
                ?? new[] { "http://localhost:5173", "http://127.0.0.1:5173" };

            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
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

    // Configure DbContext. Provider is selected by Kryptonian:Database:Provider
    // (Sqlite | PostgreSQL | InMemory); when unset it falls back to PostgreSQL if a
    // connection string is present, otherwise InMemory.
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? builder.Configuration["Kryptonian:Database:ConnectionString"];
    var configuredProvider = builder.Configuration["Kryptonian:Database:Provider"];
    var provider = configuredProvider?.Trim().ToLowerInvariant() ?? "";
    if (string.IsNullOrEmpty(provider))
    {
        provider = string.IsNullOrEmpty(connectionString) ? "inmemory" : "postgresql";
    }

    switch (provider)
    {
        case "sqlite":
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "Data Source=kryptonian.db;Cache=Shared";
            }
            builder.Services.AddDbContext<KryptonianDbContext>(options =>
                options.UseSqlite(connectionString));
            Log.Information("Using SQLite database. Connection: {Conn}", connectionString);
            break;

        case "postgresql":
        case "postgres":
        case "npgsql":
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Kryptonian:Database:Provider=PostgreSQL requires ConnectionStrings:DefaultConnection.");
            }
            // Configure Npgsql with dynamic JSON support for Dictionary<string, object> columns
            var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();
            var dataSource = dataSourceBuilder.Build();
            builder.Services.AddDbContext<KryptonianDbContext>(options =>
                options.UseNpgsql(dataSource));
            Log.Information("Using PostgreSQL database.");
            break;

        case "inmemory":
        case "in-memory":
        case "memory":
            builder.Services.AddDbContext<KryptonianDbContext>(options =>
                options.UseInMemoryDatabase("KryptonianDev"));
            Log.Warning("Using in-memory database. State will not survive process restarts.");
            break;

        default:
            throw new InvalidOperationException(
                $"Unknown Kryptonian:Database:Provider '{configuredProvider}'. Valid: Sqlite, PostgreSQL, InMemory.");
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

    // Initialize the database. Postgres uses the migration history; SQLite uses
    // EnsureCreated since our migrations are Postgres-specific; in-memory needs no
    // setup. Applies on every environment so deployments don't ship without a schema.
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<KryptonianDbContext>();
        if (dbContext.Database.IsNpgsql())
        {
            Log.Information("Applying database migrations...");
            dbContext.Database.Migrate();
            Log.Information("Database migrations applied successfully");
        }
        else if (dbContext.Database.IsSqlite())
        {
            Log.Information("Ensuring SQLite database schema...");
            dbContext.Database.EnsureCreated();
            Log.Information("SQLite schema ready");
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

    // The dashboard ships in two layouts:
    //   - Dev: ../RTSec.Kryptonian.Ui/dist (built by `npm run build` next to the API repo).
    //   - Container: /app/wwwroot/ui (copied in from the Docker ui stage).
    // The first existing path wins.
    var uiCandidatePaths = new[]
    {
        Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "ui")),
        Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "RTSec.Kryptonian.Ui", "dist")),
    };
    var uiDistPath = uiCandidatePaths.FirstOrDefault(Directory.Exists);

    if (uiDistPath != null)
    {
        Log.Information("Serving admin dashboard from {Path}", uiDistPath);
        var uiFileProvider = new PhysicalFileProvider(uiDistPath);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = uiFileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = uiFileProvider });
    }
    else
    {
        Log.Information("No dashboard bundle found; API-only mode (checked: {Paths})", string.Join(", ", uiCandidatePaths));
    }

    var allowPlainHttpEst = app.Configuration.GetValue<bool>("Kryptonian:Est:AllowPlainHttp", false);
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps
            && context.Request.Path.StartsWithSegments("/.well-known/est", StringComparison.OrdinalIgnoreCase)
            && !allowPlainHttpEst)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "EST endpoints require HTTPS. Use https://localhost:8443/.well-known/est for local development."
            });
            return;
        }

        await next();
    });

    if (app.Configuration.GetValue<bool>("Kryptonian:Tls:RedirectHttpToHttps", !app.Environment.IsDevelopment()))
    {
        app.UseHttpsRedirection();
    }
    app.UseCors("KryptonianUi");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/api/status/health");
    if (Directory.Exists(uiDistPath))
    {
        app.MapFallback(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api")
                || context.Request.Path.StartsWithSegments("/.well-known"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(Path.Combine(uiDistPath, "index.html"));
        });
    }

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
