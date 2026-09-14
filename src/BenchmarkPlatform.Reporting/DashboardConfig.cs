using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;

namespace BenchmarkPlatform.Reporting;

/// <summary>
/// The configuration every benchmark in this repository uses: memory diagnostics,
/// the usual file exporters, plus the SQLite dashboard exporter.
/// </summary>
public class DashboardConfig : ManualConfig
{
    public DashboardConfig()
    {
        // Start from the defaults (console logger, standard columns, validators) and add to them.
        Add(DefaultConfig.Instance);

        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(JsonExporter.FullCompressed);
        AddExporter(new SqliteDashboardExporter());

        WithOptions(ConfigOptions.DisableLogFile);
    }
}
