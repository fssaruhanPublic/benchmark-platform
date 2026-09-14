using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace BenchmarkPlatform.Reporting;

/// <summary>
/// Put this on a benchmark class to opt it into <see cref="DashboardConfig"/>.
/// Not required when the class is run through <c>BenchmarkSwitcher</c> with the shared config,
/// but handy when a class is executed directly.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DashboardBenchmarkAttribute : Attribute, IConfigSource
{
    public DashboardBenchmarkAttribute()
    {
        Config = new DashboardConfig();
    }

    public IConfig Config { get; }
}
