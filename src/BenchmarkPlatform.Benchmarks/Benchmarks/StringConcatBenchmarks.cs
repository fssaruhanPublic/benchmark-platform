using System.Text;
using BenchmarkDotNet.Attributes;

namespace BenchmarkPlatform.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("string")]
public class StringConcatBenchmarks
{
    [Params(10, 500)]
    public int Count { get; set; }

    private string[] _parts = [];

    [GlobalSetup]
    public void Setup() => _parts = Enumerable.Range(0, Count).Select(i => $"part-{i}").ToArray();

    [Benchmark(Baseline = true)]
    public string PlusOperator()
    {
        var result = string.Empty;
        foreach (var part in _parts)
        {
            result += part;
        }

        return result;
    }

    [Benchmark]
    public string StringBuilder()
    {
        var builder = new StringBuilder();
        foreach (var part in _parts)
        {
            builder.Append(part);
        }

        return builder.ToString();
    }

    [Benchmark]
    public string StringJoin() => string.Join(string.Empty, _parts);

    [Benchmark]
    public string StringCreate()
    {
        var length = 0;
        foreach (var part in _parts)
        {
            length += part.Length;
        }

        return string.Create(length, _parts, static (span, parts) =>
        {
            var offset = 0;
            foreach (var part in parts)
            {
                part.AsSpan().CopyTo(span[offset..]);
                offset += part.Length;
            }
        });
    }
}
