using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkPlatform.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BenchmarkPlatform.Tests;

public class ScenarioRunTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static ScenarioRunDocument Document(DateTimeOffset startedAt, double accuracy, double p95Ms,
        bool caseSuccess = true, string suite = "grafirio-eval", double caseLatency = 1200) => new()
    {
        Schema = ScenarioRunImporter.SchemaV1,
        Kind = "eval",
        Suite = suite,
        Label = $"run {accuracy}",
        StartedAt = startedAt,
        FinishedAt = startedAt.AddMinutes(3),
        GitCommit = "abc1234",
        GitBranch = "main",
        MachineName = "ci",
        Target = "http://localhost:5000",
        Environment = new() { ["model"] = "gpt-test" },
        Metrics =
        [
            new() { Name = "accuracy", Value = accuracy, Unit = "ratio", Direction = "higher" },
            new() { Name = "latency.p95", Value = p95Ms, Unit = "ms", Direction = "lower" },
            new() { Name = "questions", Value = 2, Unit = "count" }
        ],
        Cases =
        [
            new()
            {
                Name = "siparis-sayisi", Group = "aggregation", Success = caseSuccess,
                Message = caseSuccess ? null : "target_table yanlis",
                Metrics = [new() { Name = "latency.mean", Value = caseLatency, Unit = "ms", Direction = "lower" }],
                Details = JsonSerializer.SerializeToElement(new { expected = "dbo.Orders" })
            },
            new()
            {
                Name = "ulke-dagilimi", Group = "aggregation", Success = true,
                Metrics = [new() { Name = "latency.mean", Value = 900, Unit = "ms", Direction = "lower" }]
            }
        ]
    };

    // --- Validation -------------------------------------------------------------------

    [Fact]
    public void Map_RejectsWrongSchemaAndMissingFields()
    {
        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioRunImporter.Map(new ScenarioRunDocument
        {
            Schema = "something-else"
        }));

        Assert.Contains(exception.Errors, e => e.Contains("schema"));
        Assert.Contains(exception.Errors, e => e.Contains("kind"));
        Assert.Contains(exception.Errors, e => e.Contains("suite"));
        Assert.Contains(exception.Errors, e => e.Contains("startedAt"));
    }

    [Fact]
    public void Map_RejectsNonFiniteValuesDuplicateCasesAndUnknownDirection()
    {
        var document = Document(DateTimeOffset.UtcNow, 0.9, 100) with
        {
            Metrics = [new() { Name = "accuracy", Value = double.NaN, Direction = "sideways" }],
            Cases =
            [
                new() { Name = "a", Success = true },
                new() { Name = "a", Success = false }
            ]
        };

        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioRunImporter.Map(document));

        Assert.Contains(exception.Errors, e => e.Contains("finite"));
        Assert.Contains(exception.Errors, e => e.Contains("direction"));
        Assert.Contains(exception.Errors, e => e.Contains("duplicated"));
    }

    [Fact]
    public void Map_CountsPassedCasesAndLinksMetrics()
    {
        var (run, runMetrics) = ScenarioRunImporter.Map(Document(DateTimeOffset.UtcNow, 0.5, 100, caseSuccess: false));

        Assert.Equal(2, run.CaseCount);
        Assert.Equal(1, run.PassedCount);
        Assert.Equal(3, runMetrics.Count);
        Assert.All(runMetrics, m => Assert.Null(m.CaseId));
        Assert.All(run.Cases.SelectMany(c => c.Metrics), m => Assert.Equal(run.Id, m.RunId));
    }

    // --- Query service ----------------------------------------------------------------

    [Fact]
    public async Task History_ReturnsRunAndCaseMetricsOldestFirst()
    {
        using var temp = new TempDatabase();
        var day = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        await Import(temp, Document(day.AddDays(1), 0.9, 200, caseLatency: 800));
        await Import(temp, Document(day, 0.8, 300, caseSuccess: false, caseLatency: 1500));
        await Import(temp, Document(day, 0.1, 999, suite: "other-suite"));

        await using var db = temp.CreateContext();
        var service = new ScenarioQueryService(db);

        var runLevel = await service.GetHistoryAsync("grafirio-eval", "accuracy", null, 50);
        Assert.Equal([0.8, 0.9], runLevel.Points.Select(p => p.Value));
        Assert.Equal("higher", runLevel.Direction);

        var caseLevel = await service.GetHistoryAsync("grafirio-eval", "latency.mean", "siparis-sayisi", 50);
        Assert.Equal([1500d, 800d], caseLevel.Points.Select(p => p.Value));
        Assert.Equal([false, true], caseLevel.Points.Select(p => p.Success));
    }

    [Fact]
    public async Task Compare_HonoursMetricDirectionAndReportsOutcomeChanges()
    {
        using var temp = new TempDatabase();
        var day = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        var baseline = await Import(temp, Document(day, 0.80, 300, caseSuccess: true, caseLatency: 1000));
        var target = await Import(temp, Document(day.AddDays(1), 0.95, 450, caseSuccess: false, caseLatency: 1020));

        await using var db = temp.CreateContext();
        var comparison = (await new ScenarioQueryService(db).CompareAsync(baseline, target, 0.10))!;

        Row(comparison, null, "accuracy").Status.ShouldBe(ComparisonStatus.Improvement);
        Row(comparison, null, "latency.p95").Status.ShouldBe(ComparisonStatus.Regression);
        // No direction → never judged.
        Row(comparison, null, "questions").Status.ShouldBe(ComparisonStatus.Neutral);
        // 2% change is below the 10% threshold.
        Row(comparison, "siparis-sayisi", "latency.mean").Status.ShouldBe(ComparisonStatus.Neutral);

        var change = Assert.Single(comparison.OutcomeChanges);
        Assert.Equal("siparis-sayisi", change.Case);
        Assert.True(change.BaselineSuccess);
        Assert.False(change.TargetSuccess);

        // One metric regression + one case that started failing.
        Assert.Equal(2, comparison.RegressionCount);
        Assert.Equal(1, comparison.ImprovementCount);
    }

    [Fact]
    public async Task Delete_RemovesCasesAndMetrics()
    {
        using var temp = new TempDatabase();
        var id = await Import(temp, Document(DateTimeOffset.UtcNow, 0.9, 100));

        await using (var db = temp.CreateContext())
            Assert.True(await new ScenarioQueryService(db).DeleteRunAsync(id));

        await using var check = temp.CreateContext();
        Assert.Empty(check.ScenarioCases);
        Assert.Empty(check.ScenarioMetrics);
    }

    // --- HTTP -------------------------------------------------------------------------

    [Fact]
    public async Task Post_ThenGet_RoundTripsTheDocument()
    {
        using var temp = new TempDatabase();
        using var factory = CreateFactory(temp);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/scenario-runs",
            Document(DateTimeOffset.UtcNow, 0.9, 100), JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ScenarioImportResultDto>(JsonOptions))!;
        Assert.Equal(2, created.CaseCount);
        Assert.Equal(5, created.MetricCount);

        var detail = (await client.GetFromJsonAsync<ScenarioRunDetailDto>($"/api/scenario-runs/{created.Id}", JsonOptions))!;
        Assert.Equal("grafirio-eval", detail.Run.Suite);
        Assert.Equal("gpt-test", detail.Environment["model"]);
        Assert.Equal("dbo.Orders", detail.Cases.Single(c => c.Name == "siparis-sayisi")
            .Details!.Value.GetProperty("expected").GetString());

        var suites = (await client.GetFromJsonAsync<List<ScenarioSuiteDto>>("/api/scenario-runs/suites", JsonOptions))!;
        var suite = Assert.Single(suites);
        Assert.Contains("accuracy", suite.RunMetrics);
        Assert.Contains("latency.mean", suite.CaseMetrics);
    }

    [Fact]
    public async Task Post_InvalidDocument_ReturnsErrors()
    {
        using var temp = new TempDatabase();
        using var factory = CreateFactory(temp);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/scenario-runs", new { schema = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("schema", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_RequiresApiKeyWhenConfigured()
    {
        using var temp = new TempDatabase();
        using var factory = CreateFactory(temp, apiKey: "secret");
        using var client = factory.CreateClient();
        var document = Document(DateTimeOffset.UtcNow, 0.9, 100);

        var anonymous = await client.PostAsJsonAsync("/api/scenario-runs", document, JsonOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/scenario-runs")
        {
            Content = JsonContent.Create(document, options: JsonOptions)
        };
        request.Headers.Add("X-Api-Key", "secret");
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(request)).StatusCode);
    }

    // --- Helpers ----------------------------------------------------------------------

    private static async Task<Guid> Import(TempDatabase temp, ScenarioRunDocument document)
    {
        await using var db = temp.CreateContext();
        return (await new ScenarioRunImporter(db).ImportAsync(document)).Id;
    }

    private static ScenarioComparisonRowDto Row(ScenarioComparisonDto comparison, string? caseName, string metric) =>
        comparison.Rows.Single(r => r.Case == caseName && r.Metric == metric);

    private static WebApplicationFactory<Program> CreateFactory(TempDatabase temp, string? apiKey = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Benchmarks"] = temp.ConnectionString,
                    ["Ingest:ApiKey"] = apiKey
                })));
}

internal static class AssertExtensions
{
    public static void ShouldBe(this ComparisonStatus actual, ComparisonStatus expected) =>
        Assert.Equal(expected, actual);
}
