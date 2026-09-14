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

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options => options.AddPolicy(DashboardCors, policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
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
