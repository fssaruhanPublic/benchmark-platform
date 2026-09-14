namespace BenchmarkPlatform.Core.Entities;

/// <summary>
/// The measurement of a single benchmark case within a <see cref="BenchmarkRun"/>.
/// All timings are in nanoseconds.
/// </summary>
public class BenchmarkResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }
    public BenchmarkRun? Run { get; set; }

    public string Namespace { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;

    /// <summary>Stable identity used for trend charts: <c>Type.Method(params)</c>.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Rendered parameter set, empty when the benchmark has no parameters.</summary>
    public string Parameters { get; set; } = string.Empty;

    /// <summary>Comma separated BenchmarkDotNet categories.</summary>
    public string Categories { get; set; } = string.Empty;

    public bool IsBaseline { get; set; }

    /// <summary>False when BenchmarkDotNet could not produce measurements for the case.</summary>
    public bool Success { get; set; } = true;

    public double MeanNs { get; set; }
    public double ErrorNs { get; set; }
    public double StdDevNs { get; set; }
    public double MedianNs { get; set; }
    public double MinNs { get; set; }
    public double MaxNs { get; set; }
    public double P95Ns { get; set; }

    public double OpsPerSecond { get; set; }

    public double Gen0 { get; set; }
    public double Gen1 { get; set; }
    public double Gen2 { get; set; }
    public long AllocatedBytes { get; set; }

    /// <summary>Ratio against the baseline case of the same benchmark class, when one exists.</summary>
    public double? RatioToBaseline { get; set; }
}
