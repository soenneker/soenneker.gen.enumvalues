using System.Text.Json;
using BenchmarkDotNet.Attributes;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class SerializationBenchmark
{
    private Enums.ColorCode _genValue = null!;
    private ColorCodeIntellenum _intellenumValue = null!;
    private ColorCodeSmartEnum _smartEnumValue = null!;
    private JsonSerializerOptions _stjOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _genValue = Enums.ColorCode.Red;
        _intellenumValue = ColorCodeIntellenum.Red;
        _smartEnumValue = ColorCodeSmartEnum.Red;
        _stjOptions = new JsonSerializerOptions();
    }

    [Benchmark(Baseline = true)]
    public string GenEnumValues_SystemTextJson_Serialize()
    {
        return System.Text.Json.JsonSerializer.Serialize(_genValue, _stjOptions);
    }

    [Benchmark]
    public string Intellenum_SystemTextJson_Serialize()
    {
        return System.Text.Json.JsonSerializer.Serialize(_intellenumValue, _stjOptions);
    }

    [Benchmark]
    public string SmartEnum_SystemTextJson_Serialize()
    {
        return System.Text.Json.JsonSerializer.Serialize(_smartEnumValue, _stjOptions);
    }

    [Benchmark]
    public Enums.ColorCode GenEnumValues_SystemTextJson_Deserialize()
    {
        return System.Text.Json.JsonSerializer.Deserialize<Enums.ColorCode>("\"R\"", _stjOptions)!;
    }

    [Benchmark]
    public ColorCodeIntellenum Intellenum_SystemTextJson_Deserialize()
    {
        return System.Text.Json.JsonSerializer.Deserialize<ColorCodeIntellenum>("\"R\"", _stjOptions)!;
    }

    [Benchmark]
    public ColorCodeSmartEnum SmartEnum_SystemTextJson_Deserialize()
    {
        return System.Text.Json.JsonSerializer.Deserialize<ColorCodeSmartEnum>("\"R\"", _stjOptions)!;
    }

}
