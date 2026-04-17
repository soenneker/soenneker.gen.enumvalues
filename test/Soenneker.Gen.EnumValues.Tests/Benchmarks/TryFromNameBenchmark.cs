using BenchmarkDotNet.Attributes;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class TryFromNameBenchmark
{
    private readonly string _red = "Red";
    private readonly string _blue = "Blue";
    private readonly string _miss = "Unknown";

    [Benchmark(Baseline = true)]
    public (Enums.ColorCode, Enums.ColorCode) TryFromName_String_Known()
    {
        Enums.ColorCode.TryFromName(_red, out Enums.ColorCode r);
        Enums.ColorCode.TryFromName(_blue, out Enums.ColorCode b);
        return (r, b);
    }

    [Benchmark]
    public (ColorCodeIntellenum?, ColorCodeIntellenum?) Intellenum()
    {
        ColorCodeIntellenum.TryFromName(_red, out ColorCodeIntellenum? r);
        ColorCodeIntellenum.TryFromName(_blue, out ColorCodeIntellenum? b);
        return (r, b);
    }

    [Benchmark]
    public (ColorCodeSmartEnum?, ColorCodeSmartEnum?) SmartEnum()
    {
        ColorCodeSmartEnum.TryFromName(_red, out ColorCodeSmartEnum? r);
        ColorCodeSmartEnum.TryFromName(_blue, out ColorCodeSmartEnum? b);
        return (r, b);
    }

    [Benchmark]
    public bool GenEnumValues_Miss()
    {
        return Enums.ColorCode.TryFromName(_miss, out _);
    }

    [Benchmark]
    public bool Intellenum_Miss()
    {
        return ColorCodeIntellenum.TryFromName(_miss, out _);
    }

    [Benchmark]
    public bool SmartEnum_Miss()
    {
        return ColorCodeSmartEnum.TryFromName(_miss, out _);
    }
}
