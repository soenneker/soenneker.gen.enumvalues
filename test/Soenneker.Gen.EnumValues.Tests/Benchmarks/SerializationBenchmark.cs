using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Newtonsoft.Json;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class SerializationBenchmark
{
    private ColorCode _genValue = null!;
    private ColorCodeIntellenum _intellenumValue = null!;
    private ColorCodeSmartEnum _smartEnumValue = null!;
    private JsonSerializerOptions _stjOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _genValue = ColorCode.Red;
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
    public ColorCode GenEnumValues_SystemTextJson_Deserialize()
    {
        return System.Text.Json.JsonSerializer.Deserialize<ColorCode>("\"R\"", _stjOptions)!;
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

    ////    [Benchmark]
    //    public string GenEnumValues_Newtonsoft_Serialize()
    //    {
    //        return JsonConvert.SerializeObject(_genValue);
    //    }

    //  //  [Benchmark]
    //    public string Intellenum_Newtonsoft_Serialize()
    //    {
    //        return JsonConvert.SerializeObject(_intellenumValue);
    //    }

    //    //[Benchmark]
    //    public string SmartEnum_Newtonsoft_Serialize()
    //    {
    //        return JsonConvert.SerializeObject(_smartEnumValue);
    //    }

    //    //[Benchmark]
    //    public ColorCode GenEnumValues_Newtonsoft_Deserialize()
    //    {
    //        return JsonConvert.DeserializeObject<ColorCode>("\"R\"")!;
    //    }

    //  //  [Benchmark]
    //    public ColorCodeIntellenum Intellenum_Newtonsoft_Deserialize()
    //    {
    //        return JsonConvert.DeserializeObject<ColorCodeIntellenum>("\"R\"")!;
    //    }

    //   // [Benchmark]
    //    public ColorCodeSmartEnum SmartEnum_Newtonsoft_Deserialize()
    //    {
    //        return JsonConvert.DeserializeObject<ColorCodeSmartEnum>("\"R\"")!;
    //    }
}
