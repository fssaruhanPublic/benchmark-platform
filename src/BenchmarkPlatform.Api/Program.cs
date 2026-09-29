using System.Text.Json.Serialization;
using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string DashboardCors = "dashboard";

// Resolved lazily so tests (and any host that layers configuration late) can override it.
builder.Services.AddDbContext<BenchmarkDbContext>((serviceProvider, options) =>
{
    var configured = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Benchmarks");

    if (string.IsNullOrWhiteSpace(configured))
    {
        var databasePath = BenchmarkDbPath.Resolve();
        BenchmarkDbPath.EnsureDirectoryExists(databasePath);
        configured = $"Data Source={databasePath}";
    }

    options.UseSqlite(configured);
});
builder.Services.AddScoped<BenchmarkQueryService>();
builder.Services.AddScoped<ScenarioQueryService>();
builder.Services.AddScoped<ScenarioRunImporter>();
builder.Services.AddScoped<BdnJsonImporter>();

// Scenario documents can be large (thousands of eval cases with details).
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
    options.Limits.MaxRequestBodySize = 64 * 1024 * 1024);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The Vite dev server plus any consumer that reads this API from a browser (e.g. an admin
// panel showing scenario results). Cors:AllowedOrigins replaces the list entirely.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
    ? configured
    : ["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:59265", "http://localhost:59666"];

builder.Services.AddCors(options => options.AddPolicy(DashboardCors, policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// The exporter may have created the file already; Migrate() is safe either way.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>().Database.Migrate();
}

app.UseCors(DashboardCors);

// wwwroot only exists once `npm run build` has been run in src/web.
var hasBuiltFrontend = !string.IsNullOrEmpty(app.Environment.WebRootPath)
                       && File.Exists(Path.Combine(app.Environment.WebRootPath, "index.html"));

if (hasBuiltFrontend)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

var api = app.MapGroup("/api");

api.MapGet("/runs", async (BenchmarkQueryService service, CancellationToken ct,
        int skip = 0, int take = 25, string? search = null)
    => Results.Ok(await service.GetRunsAsync(skip, take, search, ct)));

api.MapGet("/runs/{id:guid}", async (Guid id, BenchmarkQueryService service, CancellationToken ct)
    => await service.GetRunAsync(id, ct) is { } run ? Results.Ok(run) : Results.NotFound());

api.MapDelete("/runs/{id:guid}", async (Guid id, BenchmarkQueryService service, CancellationToken ct)
    => await service.DeleteRunAsync(id, ct) ? Results.NoContent() : Results.NotFound());

api.MapGet("/benchmarks", async (BenchmarkQueryService service, CancellationToken ct)
    => Results.Ok(await service.GetBenchmarksAsync(ct)));

api.MapGet("/benchmarks/history", async (string fullName, BenchmarkQueryService service,
        CancellationToken ct, int limit = 50)
    => string.IsNullOrWhiteSpace(fullName)
        ? Results.BadRequest(new { error = "fullName is required." })
        : Results.Ok(await service.GetHistoryAsync(fullName, limit, ct)));

api.MapGet("/compare", async (Guid baselineRunId, Guid targetRunId, BenchmarkQueryService service,
        CancellationToken ct, double threshold = BenchmarkQueryService.DefaultThreshold)
    => await service.CompareAsync(baselineRunId, targetRunId, threshold, ct) is { } comparison
        ? Results.Ok(comparison)
        : Results.NotFound(new { error = "One of the runs was not found." }));

api.MapGet("/summary", async (BenchmarkQueryService service, CancellationToken ct)
    => Results.Ok(await service.GetSummaryAsync(ct)));

// Ingest endpoints are the only writes reachable from other machines' tools. When
// Ingest:ApiKey is configured the caller must present it; unset keeps local use simple.
static bool IngestAllowed(HttpRequest request, IConfiguration configuration)
{
    var expectedKey = configuration["Ingest:ApiKey"];
    return string.IsNullOrEmpty(expectedKey) ||
           string.Equals(request.Headers["X-Api-Key"].ToString(), expectedKey, StringComparison.Ordinal);
}

// BenchmarkDotNet's own JSON report (JsonExporter.Full), for projects that do not use the
// in-process exporter. Same runId across calls = one run (BDN writes one file per class).
api.MapPost("/runs/import/bdn", async (HttpRequest request, IConfiguration configuration, BdnJsonImporter importer,
        CancellationToken ct, Guid? runId = null, string? label = null, string? gitCommit = null,
        string? gitBranch = null, string? machineName = null) =>
{
    if (!IngestAllowed(request, configuration)) return Results.Unauthorized();

    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync(ct);

    try
    {
        var (id, imported) = await importer.ImportAsync(json, new BdnImportMetadata(runId, label, gitCommit, gitBranch, machineName), ct);
        return Results.Ok(new { id, imported });
    }
    catch (ScenarioValidationException exception)
    {
        return Results.BadRequest(new { error = "Invalid BenchmarkDotNet report.", errors = exception.Errors });
    }
});

// --- Scenario runs (eval sets, load tests): "scenario-run/v1" documents ---------------

var scenarios = api.MapGroup("/scenario-runs");

scenarios.MapPost("/", async (ScenarioRunDocument document, HttpRequest request, IConfiguration configuration,
        ScenarioRunImporter importer, CancellationToken ct) =>
{
    if (!IngestAllowed(request, configuration)) return Results.Unauthorized();

    try
    {
        var result = await importer.ImportAsync(document, ct);
        return Results.Created($"/api/scenario-runs/{result.Id}", result);
    }
    catch (ScenarioValidationException exception)
    {
        return Results.BadRequest(new { error = "Invalid scenario run.", errors = exception.Errors });
    }
});

scenarios.MapGet("/", async (ScenarioQueryService service, CancellationToken ct,
        string? kind = null, string? suite = null, int skip = 0, int take = 50)
    => Results.Ok(await service.GetRunsAsync(kind, suite, skip, take, ct)));

scenarios.MapGet("/suites", async (ScenarioQueryService service, CancellationToken ct)
    => Results.Ok(await service.GetSuitesAsync(ct)));

scenarios.MapGet("/history", async (string suite, string metric, ScenarioQueryService service,
        CancellationToken ct, string? @case = null, int limit = 100)
    => string.IsNullOrWhiteSpace(suite) || string.IsNullOrWhiteSpace(metric)
        ? Results.BadRequest(new { error = "suite and metric are required." })
        : Results.Ok(await service.GetHistoryAsync(suite, metric, string.IsNullOrWhiteSpace(@case) ? null : @case,
            limit, ct)));

scenarios.MapGet("/compare", async (Guid baselineRunId, Guid targetRunId, ScenarioQueryService service,
        CancellationToken ct, double threshold = BenchmarkQueryService.DefaultThreshold)
    => await service.CompareAsync(baselineRunId, targetRunId, threshold, ct) is { } comparison
        ? Results.Ok(comparison)
        : Results.NotFound(new { error = "One of the runs was not found." }));

scenarios.MapGet("/{id:guid}", async (Guid id, ScenarioQueryService service, CancellationToken ct)
    => await service.GetRunAsync(id, ct) is { } run ? Results.Ok(run) : Results.NotFound());

scenarios.MapDelete("/{id:guid}", async (Guid id, ScenarioQueryService service, CancellationToken ct)
    => await service.DeleteRunAsync(id, ct) ? Results.NoContent() : Results.NotFound());

app.MapGet("/health", () => Results.Ok(new { status = "ok", database = BenchmarkDbPath.Resolve() }));

if (hasBuiltFrontend)
{
    // Client-side routing: anything that is not /api or a real file falls back to the SPA shell.
    app.MapFallbackToFile("index.html");
}
else
{
    app.MapGet("/", () => Results.Text(
        "Dashboard arayuzu henuz derlenmedi. 'cd src/web && npm install && npm run build' calistirin "
        + "ya da gelistirme icin 'npm run dev' ile http://localhost:5173 adresini kullanin.",
        "text/plain; charset=utf-8"));
}

app.Run();

/// <summary>Exposed for <c>WebApplicationFactory</c> in the test project.</summary>
public partial class Program;
