namespace BenchmarkPlatform.Core.Data;

/// <summary>
/// Resolves the single SQLite file shared by the benchmark exporter and the dashboard API.
/// </summary>
public static class BenchmarkDbPath
{
    public const string EnvironmentVariable = "BENCHMARK_DB_PATH";

    // Matches both the classic .sln and the .slnx format.
    private const string SolutionSearchPattern = "BenchmarkPlatform.sln*";

    /// <summary>
    /// Returns the absolute path of the database file, honouring <c>BENCHMARK_DB_PATH</c>
    /// and otherwise falling back to <c>&lt;repo root&gt;/data/benchmarks.db</c>.
    /// </summary>
    public static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        var root = FindRepositoryRoot() ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, "data", "benchmarks.db");
    }

    public static string ConnectionString() => $"Data Source={Resolve()}";

    public static void EnsureDirectoryExists(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string? FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (directory.GetFiles(SolutionSearchPattern).Length > 0)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
