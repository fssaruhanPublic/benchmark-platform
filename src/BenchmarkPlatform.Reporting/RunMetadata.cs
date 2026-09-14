using System.Diagnostics;

namespace BenchmarkPlatform.Reporting;

/// <summary>
/// Ambient information attached to every stored run: the optional <c>--label</c> and git details.
/// </summary>
public static class RunMetadata
{
    /// <summary>Set from the command line before the benchmarks start.</summary>
    public static string? Label { get; set; }

    /// <summary>
    /// Identifies this process. BenchmarkDotNet exports one summary per benchmark class, and all of
    /// them belong to the same dashboard run.
    /// </summary>
    public static Guid SessionId { get; } = Guid.NewGuid();

    public static string? GitCommit()
        => Environment.GetEnvironmentVariable("GIT_COMMIT") ?? Git("rev-parse --short HEAD");

    public static string? GitBranch()
        => Environment.GetEnvironmentVariable("GIT_BRANCH") ?? Git("rev-parse --abbrev-ref HEAD");

    private static string? Git(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            });

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);

            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch
        {
            // Not a git checkout, or git is not installed — metadata is optional.
            return null;
        }
    }
}
