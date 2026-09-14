using BenchmarkDotNet.Running;
using BenchmarkPlatform.Reporting;

// Usage:
//   dotnet run -c Release --project src/BenchmarkPlatform.Benchmarks -- --filter *StringConcat*
//   dotnet run -c Release --project src/BenchmarkPlatform.Benchmarks -- --label "before caching"
//
// Every BenchmarkDotNet argument (--filter, --job, --list, ...) still works; --label is ours
// and is stored with the run so the dashboard can name it.

var (label, benchmarkArgs) = ExtractLabel(args);
RunMetadata.Label = label;

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(benchmarkArgs, new DashboardConfig());

static (string? Label, string[] Remaining) ExtractLabel(string[] args)
{
    var remaining = new List<string>(args.Length);
    string? label = null;

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--label" && i + 1 < args.Length)
        {
            label = args[++i];
            continue;
        }

        if (args[i].StartsWith("--label=", StringComparison.Ordinal))
        {
            label = args[i]["--label=".Length..];
            continue;
        }

        remaining.Add(args[i]);
    }

    return (string.IsNullOrWhiteSpace(label) ? null : label.Trim(), remaining.ToArray());
}

/// <summary>Exposed so <c>BenchmarkSwitcher</c> can find the benchmark assembly.</summary>
public partial class Program;
