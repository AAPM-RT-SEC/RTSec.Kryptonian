using System.Text.Json;
using RTSec.Kryptonian.CaHarness.Backends;
using RTSec.Kryptonian.CaHarness.Models;
using RTSec.Kryptonian.CaHarness.Scoring;

var builder = WebApplication.CreateBuilder(args);

var acmeUpstreamUrl = Environment.GetEnvironmentVariable("ACME_UPSTREAM_URL") ?? "https://step-ca:9000";
var acmeHarnessBaseUrl = Environment.GetEnvironmentVariable("ACME_HARNESS_BASE_URL");
var orthancUrl = Environment.GetEnvironmentVariable("ORTHANC_URL") ?? "http://orthanc:8042";
var internalApiKey = Environment.GetEnvironmentVariable("INTERNAL_API_KEY") ?? "changeme";

builder.Services.AddSingleton(new TeamBackendRegistry(acmeUpstreamUrl, acmeHarnessBaseUrl));
builder.Services.AddSingleton<ScoreboardService>();
builder.Services.AddSingleton<DicomVerificationService>();
builder.Services.AddHttpClient("orthanc", c => c.BaseAddress = new Uri(orthancUrl));

var app = builder.Build();
var registry = app.Services.GetRequiredService<TeamBackendRegistry>();
var scoreboard = app.Services.GetRequiredService<ScoreboardService>();
var dicomVerifier = app.Services.GetRequiredService<DicomVerificationService>();

// ── Global ────────────────────────────────────────────────────────────────────

app.MapGet("/", () =>
{
    var stream = typeof(Program).Assembly
        .GetManifestResourceStream("RTSec.Kryptonian.CaHarness.landing.html");
    if (stream is null) return Results.NotFound();
    using var reader = new StreamReader(stream);
    return Results.Content(reader.ReadToEnd(), "text/html; charset=utf-8");
});

app.MapGet("/img/mediate.png", () =>
{
    var stream = typeof(Program).Assembly
        .GetManifestResourceStream("RTSec.Kryptonian.CaHarness.mediate.png");
    if (stream is null) return Results.NotFound();
    return Results.Stream(stream, "image/png");
});

app.MapGet("/img/rtsec-logo.png", () =>
{
    var stream = typeof(Program).Assembly
        .GetManifestResourceStream("RTSec.Kryptonian.CaHarness.rtsec-logo.png");
    if (stream is null) return Results.NotFound();
    return Results.Stream(stream, "image/png");
});

app.MapGet("/health", () =>
    Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/api/backends", () =>
    Results.Ok(new[] { "selfsigned", "adcs", "ejbca", "acme" }));

app.MapGet("/api/teams", () =>
    Results.Ok(registry.GetTeamNames()));

app.MapPost("/api/teams/register", (RegisterRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.TeamName))
        return Results.BadRequest(new { error = "teamName is required" });
    var (token, teamName) = registry.Register(req.TeamName.Trim());
    return Results.Ok(new { token, teamName });
});

app.MapGet("/api/help", () =>
{
    var stream = typeof(Program).Assembly
        .GetManifestResourceStream("RTSec.Kryptonian.CaHarness.USER_GUIDE.md");
    if (stream is null)
        return Results.Problem("README not found in assembly resources.");
    using var reader = new StreamReader(stream);
    var markdown = reader.ReadToEnd();
    var mdJson = JsonSerializer.Serialize(markdown);
    var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>CA Harness — API Guide</title>
          <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/github-markdown-css@5/github-markdown.min.css">
          <style>
            body { background: #0d1117; }
            .markdown-body {
              box-sizing: border-box;
              min-width: 200px;
              max-width: 960px;
              margin: 0 auto;
              padding: 45px;
              color-scheme: dark;
            }
            @media (max-width: 767px) { .markdown-body { padding: 15px; } }
          </style>
        </head>
        <body>
          <article id="content" class="markdown-body"></article>
          <script src="https://cdn.jsdelivr.net/npm/marked@9/marked.min.js"></script>
          <script>
            document.getElementById('content').innerHTML = marked.parse({{mdJson}});
          </script>
        </body>
        </html>
        """;
    return Results.Content(html, "text/html; charset=utf-8");
});

// ── Scoreboard ────────────────────────────────────────────────────────────────

app.MapGet("/api/scoreboard", () =>
    Results.Ok(scoreboard.GetScoreboard()));

app.MapGet("/scoreboard", () =>
{
    var scores = scoreboard.GetScoreboard();
    var rows = string.Join("\n", scores.Select(s =>
    {
        var backendCells = string.Join("", s.Backends.Select(b =>
        {
            var (bg, label) = b.Status switch
            {
                "dicom_complete" => ("#238636", "✓ DICOM"),
                "enrolled" => ("#9e6a03", "Enrolled"),
                _ => ("#30363d", "Not Started")
            };
            var gw = b.UsedGateway ? " <span title='EST gateway verified' style='color:#58a6ff'>⇌</span>" : "";
            var dimse = b.DimseStoreTlsComplete ? " <span title='DIMSE mTLS C-STORE complete' style='color:#3fb950'>🔒</span>" : "";
            return $"<td style='text-align:center'><span style='background:{bg};color:#fff;padding:2px 8px;border-radius:4px;font-size:12px'>{label}</span>{gw}{dimse}</td>";
        }));
        var firstDicom = s.FirstDicomUtc.HasValue
            ? s.FirstDicomUtc.Value.ToString("HH:mm:ss UTC")
            : "—";
        var rankBadge = s.OverallRank == 1 ? "🥇" : s.OverallRank == 2 ? "🥈" : s.OverallRank == 3 ? "🥉" : $"#{s.OverallRank}";
        return $"<tr><td style='font-weight:bold;padding:8px 12px'>{rankBadge}</td><td style='padding:8px 12px'>{HtmlEncode(s.TeamName)}</td>{backendCells}<td style='text-align:center;padding:8px 12px'>{s.BackendsComplete}/4</td><td style='padding:8px 12px;color:#8b949e'>{firstDicom}</td></tr>";
    }));

    var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta http-equiv="refresh" content="10">
          <title>Kryptonian Hackathon — Leaderboard</title>
          <style>
            body { background:#0d1117; color:#e6edf3; font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif; margin:0; padding:24px; }
            h1 { color:#58a6ff; font-size:24px; margin-bottom:4px; }
            .subtitle { color:#8b949e; font-size:13px; margin-bottom:24px; }
            table { border-collapse:collapse; width:100%; max-width:960px; }
            th { background:#161b22; color:#8b949e; font-size:12px; text-transform:uppercase; padding:8px 12px; text-align:left; border-bottom:1px solid #30363d; }
            td { border-bottom:1px solid #21262d; vertical-align:middle; }
            tr:hover td { background:#161b22; }
          </style>
        </head>
        <body>
          <h1>Kryptonian Hackathon Leaderboard</h1>
          <div class="subtitle">Auto-refreshes every 10 seconds · {{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}} UTC</div>
          <table>
            <thead><tr>
              <th>Rank</th><th>Team</th>
              <th style="text-align:center">Self-Signed</th>
              <th style="text-align:center">ADCS</th>
              <th style="text-align:center">EJBCA</th>
              <th style="text-align:center">ACME</th>
              <th style="text-align:center">Complete</th>
              <th>First DICOM</th>
            </tr></thead>
            <tbody>{{rows}}</tbody>
          </table>
          <div style="margin-top:16px;color:#8b949e;font-size:12px">⇌ = EST gateway verified &nbsp;·&nbsp; ✓ DICOM = DICOMweb transfer received &nbsp;·&nbsp; 🔒 = DIMSE mTLS C-STORE complete</div>
          <div style="margin-top:12px;color:#8b949e;font-size:12px">
            📥 <a href="https://stkryptonianfiles.blob.core.windows.net/downloads/dicom-examples.zip" style="color:#58a6ff" download>Download DICOM test files (28 MB)</a> — 138 CT instances to use as your DICOM payload
          </div>
        </body>
        </html>
        """;
    return Results.Content(html, "text/html; charset=utf-8");
});

// ── Per-team backend endpoints ────────────────────────────────────────────────

app.MapPost("/teams/{token}/api/backends/{backend}/reset", (string token, string backend) =>
{
    var team = registry.GetByToken(token);
    if (team is null) return Results.NotFound(new { error = "Unknown team token" });
    scoreboard.RecordReset(token, backend);
    if (backend.Equals("acme", StringComparison.OrdinalIgnoreCase))
    {
        team.Backends.Acme.Reset();
        return Results.Ok(new { backend = "acme", reset = true, timestamp = DateTime.UtcNow });
    }
    var b = team.Backends.ResolveBackend(backend);
    if (b is null) return Results.NotFound(new { error = $"Unknown backend: {backend}" });
    b.Reset();
    return Results.Ok(new { backend, reset = true, timestamp = DateTime.UtcNow });
});

app.MapGet("/teams/{token}/api/backends/{backend}/cacerts", (string token, string backend) =>
{
    var team = registry.GetByToken(token);
    if (team is null) return Results.NotFound(new { error = "Unknown team token" });
    var b = team.Backends.ResolveBackend(backend);
    if (b is null) return Results.NotFound(new { error = $"Unknown backend: {backend}" });
    return Results.Ok(new[] { b.GetCaCertificatePem() });
});

app.MapPost("/teams/{token}/api/backends/{backend}/issue",
    (string token, string backend, IssueRequest request) =>
    {
        var team = registry.GetByToken(token);
        if (team is null) return Results.NotFound(new { error = "Unknown team token" });
        var b = team.Backends.ResolveBackend(backend);
        if (b is null) return Results.NotFound(new { error = $"Unknown backend: {backend}" });
        return Results.Ok(b.Issue(request));
    });

app.MapPost("/teams/{token}/api/backends/{backend}/revoke",
    (string token, string backend, RevokeRequest request) =>
    {
        var team = registry.GetByToken(token);
        if (team is null) return Results.NotFound(new { error = "Unknown team token" });
        var b = team.Backends.ResolveBackend(backend);
        if (b is null) return Results.NotFound(new { error = $"Unknown backend: {backend}" });
        var ok = b.Revoke(request.SerialNumber);
        return ok
            ? Results.Ok(new { revoked = true, serialNumber = request.SerialNumber })
            : Results.NotFound(new { error = $"Certificate not found: {request.SerialNumber}" });
    });

app.MapGet("/teams/{token}/api/backends/{backend}/issued", (string token, string backend) =>
{
    var team = registry.GetByToken(token);
    if (team is null) return Results.NotFound(new { error = "Unknown team token" });
    if (backend.Equals("acme", StringComparison.OrdinalIgnoreCase))
        return Results.Ok(team.Backends.Acme.GetIssued());
    var b = team.Backends.ResolveBackend(backend);
    if (b is null) return Results.NotFound(new { error = $"Unknown backend: {backend}" });
    return Results.Ok(b.GetIssued());
});

app.MapGet("/teams/{token}/api/backends/acme/activity", (string token) =>
{
    var team = registry.GetByToken(token);
    if (team is null) return Results.NotFound(new { error = "Unknown team token" });
    return Results.Ok(team.Backends.Acme.GetActivity());
});

// ADCS-only: release a pending approval
app.MapPost("/teams/{token}/api/backends/adcs/approve/{requestId}",
    (string token, string requestId) =>
    {
        var team = registry.GetByToken(token);
        if (team is null) return Results.NotFound(new { error = "Unknown team token" });
        var response = team.Backends.Adcs.Approve(requestId);
        return response is not null
            ? Results.Ok(response)
            : Results.NotFound(new { error = $"Pending request not found: {requestId}" });
    });

// ── Internal API (DIMSE TLS proxy callbacks) ──────────────────────────────────
// Protected by X-Internal-Key header. Only the DIMSE proxy on the VM calls these.

app.MapPost("/api/internal/dimse/verify-cert", async (HttpContext context) =>
{
    if (context.Request.Headers["X-Internal-Key"].FirstOrDefault() != internalApiKey)
        return Results.StatusCode(403);

    var body = await context.Request.ReadFromJsonAsync<JsonElement>();
    var certB64 = body.GetProperty("certBase64Der").GetString() ?? "";

    // Try every team × every non-ACME backend to find which CA signed this cert
    foreach (var team in registry.GetAllTeams())
    {
        foreach (var backendId in new[] { "selfsigned", "adcs", "ejbca" })
        {
            var b = team.Backends.ResolveBackend(backendId);
            if (b is null) continue;
            var result = dicomVerifier.Verify(team, backendId, certB64);
            if (result is not null)
            {
                return Results.Ok(new
                {
                    teamToken = team.Token,
                    teamName = team.TeamName,
                    backend = backendId,
                    serialNumber = result.SerialNumber,
                    thumbprint = result.Thumbprint
                });
            }
        }
    }
    return Results.NotFound(new { error = "Certificate not issued by any known team CA" });
});

app.MapPost("/api/internal/dimse/cstore-event", async (HttpContext context) =>
{
    if (context.Request.Headers["X-Internal-Key"].FirstOrDefault() != internalApiKey)
        return Results.StatusCode(403);

    var body = await context.Request.ReadFromJsonAsync<JsonElement>();
    var teamToken = body.GetProperty("teamToken").GetString() ?? "";
    var backend = body.GetProperty("backend").GetString() ?? "";

    if (registry.GetByToken(teamToken) is null)
        return Results.NotFound(new { error = "Unknown team token" });

    scoreboard.RecordDimseStoreTls(teamToken, backend);
    return Results.Ok(new { recorded = true });
});

// ── EST enrollment endpoints ──────────────────────────────────────────────────
// Implements RFC 7030 simpleenroll / simplereenroll for gateway-proxied enrollment.
// Certs issued here embed the EST OID (1.3.6.1.4.1.99999.1) — cryptographic proof
// that enrollment passed through a gateway. The JSON /issue path never sets this OID.

app.MapMethods("/teams/{token}/est/{backend}/simpleenroll",
    ["GET", "POST"],
    async (HttpContext context, string token, string backend) =>
    {
        var team = registry.GetByToken(token);
        if (team is null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "Unknown team token" });
            return;
        }

        var b = team.Backends.ResolveBackend(backend);
        if (b is null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = $"Unknown backend: {backend}" });
            return;
        }

        var contentType = context.Request.ContentType ?? "";
        if (!contentType.Contains("application/pkcs10", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 415;
            await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/pkcs10" });
            return;
        }

        string csrBase64;
        using (var sr = new StreamReader(context.Request.Body))
            csrBase64 = (await sr.ReadToEndAsync()).Trim();

        var deviceId = context.Request.Headers["X-Device-Id"].FirstOrDefault() ?? "";
        var result = b.IssueViaEst(csrBase64, deviceId);

        context.Response.StatusCode = result.Status == "issued" ? 200 : 400;
        await context.Response.WriteAsJsonAsync(result);
    });

app.MapMethods("/teams/{token}/est/{backend}/simplereenroll",
    ["GET", "POST"],
    async (HttpContext context, string token, string backend) =>
    {
        var team = registry.GetByToken(token);
        if (team is null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "Unknown team token" });
            return;
        }

        var b = team.Backends.ResolveBackend(backend);
        if (b is null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = $"Unknown backend: {backend}" });
            return;
        }

        var contentType = context.Request.ContentType ?? "";
        if (!contentType.Contains("application/pkcs10", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 415;
            await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/pkcs10" });
            return;
        }

        string csrBase64;
        using (var sr = new StreamReader(context.Request.Body))
            csrBase64 = (await sr.ReadToEndAsync()).Trim();

        var deviceId = context.Request.Headers["X-Device-Id"].FirstOrDefault() ?? "";
        var result = b.IssueViaEst(csrBase64, deviceId);

        context.Response.StatusCode = result.Status == "issued" ? 200 : 400;
        await context.Response.WriteAsJsonAsync(result);
    });

// ── DICOM submission endpoint ─────────────────────────────────────────────────

app.MapPost("/teams/{token}/dicom/backends/{backend}/stow",
    async (HttpContext context, string token, string backend, IHttpClientFactory httpClientFactory) =>
    {
        var team = registry.GetByToken(token);
        if (team is null)
            return Results.NotFound(new { error = "Unknown team token" });

        if (backend.Equals("acme", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "ACME backend does not support DICOM STOW — ACME completion is tracked automatically" });

        var b = team.Backends.ResolveBackend(backend);
        if (b is null)
            return Results.NotFound(new { error = $"Unknown backend: {backend}" });

        var certPem = context.Request.Headers["X-Device-Certificate"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(certPem))
            return Results.BadRequest(new { error = "X-Device-Certificate header is required (PEM-encoded)" });

        var verification = dicomVerifier.Verify(team, backend, certPem);
        if (verification is null)
            return Results.Unauthorized();

        // Forward to Orthanc (best-effort — scoring does not depend on Orthanc success)
        string sopInstanceUid = "";
        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            sopInstanceUid = ExtractSopInstanceUid(body);

            var orthancClient = httpClientFactory.CreateClient("orthanc");
            using var content = new StringContent(body, System.Text.Encoding.UTF8, context.Request.ContentType ?? "application/dicom");
            await orthancClient.PostAsync("/instances", content);
        }
        catch
        {
            // Orthanc forwarding is best-effort
        }

        var record = new DicomTransferRecord(
            Token: token,
            Backend: backend,
            DeviceId: verification.DeviceId,
            SerialNumber: verification.SerialNumber,
            Thumbprint: verification.Thumbprint,
            UsedGateway: verification.UsedGateway,
            ReceivedUtc: DateTime.UtcNow,
            SopInstanceUid: sopInstanceUid);

        scoreboard.RecordTransfer(record);

        return Results.Ok(new
        {
            accepted = true,
            serialNumber = verification.SerialNumber,
            sopInstanceUid,
            usedGateway = verification.UsedGateway
        });
    });

// ── ACME transparent proxy ─────────────────────────────────────────────────────
// Registered after all /api routes so the more-specific routes above take precedence.
// Strips /teams/{token} from the path before forwarding so step-ca sees /acme/...
app.MapMethods("/teams/{token}/acme/{**rest}",
    ["GET", "POST", "HEAD", "PUT", "DELETE"],
    async (HttpContext context, string token) =>
    {
        var team = registry.GetByToken(token);
        if (team is null)
        {
            context.Response.StatusCode = 404;
            return;
        }
        // Map /teams/{token}/acme/{rest} → /acme/acme/{rest} on step-ca.
        var rest = context.Request.RouteValues["rest"] as string ?? "";
        context.Request.Path = rest.Length > 0 ? "/acme/acme/" + rest : "/acme/acme";
        await team.Backends.Acme.ProxyAsync(context);
    });

app.Run();

static string ExtractSopInstanceUid(string body)
{
    // Simple heuristic: look for SOP Instance UID tag (0008,0018) in the body text
    var marker = "0008,0018";
    var idx = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    if (idx < 0) return "";
    var valueStart = body.IndexOf('"', idx + marker.Length);
    if (valueStart < 0) return "";
    var valueEnd = body.IndexOf('"', valueStart + 1);
    if (valueEnd < 0) return "";
    return body[(valueStart + 1)..valueEnd];
}

static string HtmlEncode(string s) =>
    s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

// Allow WebApplicationFactory to find the entry point assembly in tests
public partial class Program { }
