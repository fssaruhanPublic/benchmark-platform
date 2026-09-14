using BenchmarkDotNet.Attributes;

namespace BenchmarkPlatform.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("linq")]
public class LinqVsLoopBenchmarks
{
    private int[] _numbers = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(1337);
        _numbers = Enumerable.Range(0, 10_000).Select(_ => random.Next(0, 1_000)).ToArray();
    }

    [Benchmark(Baseline = true)]
    public long LinqChain() => _numbers.Where(n => n % 2 == 0).Select(n => (long)n * 2).Sum();

    [Benchmark]
    public long ForLoop()
    {
        long sum = 0;
        for (var i = 0; i < _numbers.Length; i++)
        {
            if (_numbers[i] % 2 == 0)
            {
                sum += _numbers[i] * 2L;
            }
        }

        return sum;
    }

    [Benchmark]
    public long SpanLoop()
    {
        long sum = 0;
        ReadOnlySpan<int> span = _numbers;
        foreach (var value in span)
        {
            if (value % 2 == 0)
            {
                sum += value * 2L;
            }
        }

        return sum;
    }
}
