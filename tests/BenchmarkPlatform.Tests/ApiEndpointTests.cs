using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkPlatform.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BenchmarkPlatform.Tests;

public class ApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static WebApplicationFactory<Program> CreateFactory(TempDatabase temp)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Benchmarks"] = temp.ConnectionString
                })));

    [Fact]
    public async Task Health_ReportsOk()
    {
        using var temp = new TempDatabase();
        using var factory = CreateFactory(temp);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Runs_ReturnsSeededRuns()
    {
        using var temp = new TempDatabase();
        temp.SeedRun(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), "seeded", ("Fast", 100, 0));

        using var factory = CreateFactory(temp);
        using var client = factory.CreateClient();

        var list = await client.GetFromJsonAsync<RunListDto>("/api/runs");

        Assert.NotNull(list);
        Assert.Equal(1, list!.Total);
        Assert.Equal("seeded", list.Items[0].Label);
    }

    [Fact]
    public async Task Run_ReturnsNotFoundForUnknownId()
    {
        using var temp = new TempDatabase();
        using var factory = CreateFactory(temp);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/runs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Compare_ReturnsDeltasForTwoRuns()
    {
        using var temp = new TempDatabase();
        var baseline = temp.SeedRun(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), "baseline", ("Fast", 100, 0));
        var target = temp.SeedRun(new DateTimeOffset(2026, 9, 2, 8, 0, 0, TimeSpan.Zero), "target", ("Fast", 150, 0));

        using var factory = CreateFactory(temp);
        using var client = factory.CreateClient();

        var comparison = await client.GetFromJsonAsync<ComparisonDto>(
            $"/api/compare?baselineRunId={baseline.Id}&targetRunId={target.Id}", JsonOptions);

        Assert.NotNull(comparison);
        Assert.Equal(1, comparison!.RegressionCount);
        Assert.Equal(0.5, comparison.Rows.Single().MeanDeltaPercent!.Value, 6);
    }

    [Fact]
    public async Task History_RequiresFullName()
    {
        using var temp = new TempDatabase();
        using var factory = CreateFactory(temp);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/benchmarks/history?fullName=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRun_RemovesIt()
    {
        using var temp = new TempDatabase();
        var run = temp.SeedRun(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), "seeded", ("Fast", 100, 0));

        using var factory = CreateFactory(temp);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/runs/{run.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/runs/{run.Id}")).StatusCode);
    }
}
