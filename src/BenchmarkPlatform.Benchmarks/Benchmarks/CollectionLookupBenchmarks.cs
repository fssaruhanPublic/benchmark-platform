using BenchmarkDotNet.Attributes;

namespace BenchmarkPlatform.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("collections")]
public class CollectionLookupBenchmarks
{
    [Params(100, 10_000)]
    public int Size { get; set; }

    private List<int> _list = [];
    private HashSet<int> _hashSet = [];
    private Dictionary<int, int> _dictionary = [];
    private int[] _lookups = [];

    [GlobalSetup]
    public void Setup()
    {
        var values = Enumerable.Range(0, Size).ToArray();
        _list = [.. values];
        _hashSet = [.. values];
        _dictionary = values.ToDictionary(v => v, v => v);

        var random = new Random(42);
        _lookups = Enumerable.Range(0, 100).Select(_ => random.Next(0, Size)).ToArray();
    }

    [Benchmark(Baseline = true)]
    public int ListContains()
    {
        var hits = 0;
        foreach (var value in _lookups)
        {
            if (_list.Contains(value))
            {
                hits++;
            }
        }

        return hits;
    }

    [Benchmark]
    public int HashSetContains()
    {
        var hits = 0;
        foreach (var value in _lookups)
        {
            if (_hashSet.Contains(value))
            {
                hits++;
            }
        }

        return hits;
    }

    [Benchmark]
    public int DictionaryTryGetValue()
    {
        var hits = 0;
        foreach (var value in _lookups)
        {
            if (_dictionary.TryGetValue(value, out _))
            {
                hits++;
            }
        }

        return hits;
    }
}
