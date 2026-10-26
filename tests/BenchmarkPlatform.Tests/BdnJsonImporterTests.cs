using System.Net;
using System.Net.Http.Json;
using BenchmarkPlatform.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BenchmarkPlatform.Tests;

/// <summary>
/// Imports a real BenchmarkDotNet report (captured from a run, see Fixtures) — a hand-made
/// sample would only confirm this code's own assumptions about the format.
/// </summary>
public class BdnJsonImporterTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "bdn-report-full.json"));

    [Fact]
    public void Map_ReadsStatisticsMemoryAndParameters()
    {
        var run = BdnJsonImporter.Map(Fixture(), new BdnImportMetadata(null, "import", "abc123", "main"));

        Assert.Equal(3, run.Results.Count);
        Assert.Equal("X64", run.Architecture);
        Assert.Equal("RELEASE", run.BuildConfiguration);
        Assert.Equal("abc123", run.GitCommit);

        var one = run.Results.Single(r => r.Parameters == "Chunks=1");
        Assert.Equal("DictionaryMergeBenchmarks.Combine(Chunks=1)", one.FullName);
        Assert.True(one.Success);
        Assert.InRange(one.MeanNs, 1_000, 10_000_000);
        Assert.True(one.P95Ns >= one.MinNs);
        // BenchmarkDotNet's own table reports Gen0 = 32.2266 per 1k ops for this case.
        Assert.Equal(32.2266, one.Gen0, 3);
        Assert.Equal(406478, one.AllocatedBytes);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"Title":"x"}""")]
    public void Map_RejectsSomethingThatIsNotAReport(string json) =>
        Assert.Throws<ScenarioValidationException>(() => BdnJsonImporter.Map(json, new BdnImportMetadata(null, null, null, null)));

    [Fact]
    public async Task Post_SameRunIdAppendsToOneRun()
    {
        using var temp = new TempDatabase();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Benchmarks"] = temp.ConnectionString })));
        using var client = factory.CreateClient();
        var runId = Guid.NewGuid();

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsync($"/api/runs/import/bdn?runId={runId}&label=grafirio",
                new StringContent(Fixture(), System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using var db = temp.CreateContext();
        var run = await db.Runs.Include(r => r.Results).SingleAsync();
        Assert.Equal(runId, run.Id);
        Assert.Equal("grafirio", run.Label);
        Assert.Equal(6, run.Results.Count);

        var detail = await client.GetFromJsonAsync<RunDetailDto>($"/api/runs/{runId}");
        Assert.Equal(6, detail!.Results.Count);
    }
}
