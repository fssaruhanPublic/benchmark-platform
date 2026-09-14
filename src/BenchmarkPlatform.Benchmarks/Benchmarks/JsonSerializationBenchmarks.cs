using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;

namespace BenchmarkPlatform.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("json")]
public class JsonSerializationBenchmarks
{
    private Order[] _orders = [];
    private string _json = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _orders = Enumerable.Range(1, 200)
            .Select(i => new Order(i, $"customer-{i}", i * 12.5m, DateTimeOffset.UnixEpoch.AddDays(i), i % 3 == 0))
            .ToArray();

        _json = JsonSerializer.Serialize(_orders);
    }

    [Benchmark(Baseline = true)]
    public string SerializeReflection() => JsonSerializer.Serialize(_orders);

    [Benchmark]
    public string SerializeSourceGenerated()
        => JsonSerializer.Serialize(_orders, OrderJsonContext.Default.OrderArray);

    [Benchmark]
    public Order[]? DeserializeReflection() => JsonSerializer.Deserialize<Order[]>(_json);

    [Benchmark]
    public Order[]? DeserializeSourceGenerated()
        => JsonSerializer.Deserialize(_json, OrderJsonContext.Default.OrderArray);
}

public record Order(int Id, string Customer, decimal Total, DateTimeOffset PlacedAt, bool Express);

[JsonSerializable(typeof(Order[]))]
public partial class OrderJsonContext : JsonSerializerContext;
