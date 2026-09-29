using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BenchmarkPlatform.Tests;

/// <summary>
/// The hosted configuration (Database:Provider = SqlServer) against a real SQL Server — LocalDB
/// on Windows. SQL Server differs from SQLite exactly where it matters here: it rejects multiple
/// cascade paths, so migrations, import and delete are all exercised for real.
///
/// Without LocalDB (non-Windows CI) the test returns early and says so.
/// </summary>
public sealed class SqlServerProviderTests
{
    private const string Server = @"(localdb)\MSSQLLocalDB";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Migrate_import_query_and_delete_work_on_sql_server()
    {
        var database = $"BenchPlatformTest_{Guid.NewGuid():N}";
        var connectionString = $"Server={Server};Database={database};Trusted_Connection=True;TrustServerCertificate=True";

        try
        {
            await using (var probe = new SqlConnection($"Server={Server};Database=master;Trusted_Connection=True;"))
                await probe.OpenAsync();
        }
        catch (Exception exception) when (exception is SqlException or PlatformNotSupportedException)
        {
            Console.WriteLine($"LocalDB yok, test atlandi: {exception.Message}");
            return;
        }

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:Provider", "SqlServer");
                builder.UseSetting("ConnectionStrings:Benchmarks", connectionString);
            });
            using var client = factory.CreateClient();

            var health = await client.GetFromJsonAsync<JsonElement>("/health");
            Assert.Equal("sqlserver", health.GetProperty("database").GetString());

            var document = new ScenarioRunDocument
            {
                Schema = ScenarioRunImporter.SchemaV1, Kind = "semantic", Suite = "semantic.relationships",
                StartedAt = DateTimeOffset.UtcNow,
                Metrics = [new() { Name = "f1", Value = 0.5, Unit = "ratio", Direction = "higher" }],
                Cases =
                [
                    new() { Name = "a", Success = true, Metrics = [new() { Name = "found", Value = 1, Unit = "ratio" }] },
                    new() { Name = "b", Success = false, Details = JsonSerializer.SerializeToElement(new { x = 1 }) }
                ]
            };
            var created = await client.PostAsJsonAsync("/api/scenario-runs", document, JsonOptions);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<ScenarioImportResultDto>(JsonOptions))!.Id;

            var list = await client.GetFromJsonAsync<ScenarioRunListDto>("/api/scenario-runs?suite=semantic.relationships", JsonOptions);
            Assert.Equal(0.5, Assert.Single(list!.Items).Metrics.Single(m => m.Name == "f1").Value);

            // Run → case and run → metric cascade; case → metric does not (SQL Server). A delete must
            // still remove everything.
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/scenario-runs/{id}")).StatusCode);

            await using var db = SqlServerBenchmarkDbContextFactory.Create(connectionString);
            Assert.Equal(0, await db.ScenarioCases.CountAsync());
            Assert.Equal(0, await db.ScenarioMetrics.CountAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var master = new SqlConnection($"Server={Server};Database=master;Trusted_Connection=True;");
            await master.OpenAsync();
            await using var drop = new SqlCommand(
                $"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END",
                master);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
