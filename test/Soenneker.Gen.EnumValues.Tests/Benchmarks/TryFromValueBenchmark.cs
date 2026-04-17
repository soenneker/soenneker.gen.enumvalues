using BenchmarkDotNet.Attributes;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class TryFromValueBenchmark
{
    private readonly string _r = "R";
    private readonly string _b = "B";
    private readonly string _miss = "X";

    [Benchmark(Baseline = true)]
    public (Enums.ColorCode, Enums.ColorCode) TryFromValue_String_Known()
    {
        Enums.ColorCode.TryFromValue(_r, out Enums.ColorCode r);
        Enums.ColorCode.TryFromValue(_b, out Enums.ColorCode b);
        return (r, b);
    }

    [Benchmark]
    public (ColorCodeIntellenum?, ColorCodeIntellenum?) Intellenum()
    {
        ColorCodeIntellenum.TryFromValue(_r, out ColorCodeIntellenum? r);
        ColorCodeIntellenum.TryFromValue(_b, out ColorCodeIntellenum? b);
        return (r, b);
    }

    [Benchmark]
    public (ColorCodeSmartEnum?, ColorCodeSmartEnum?) SmartEnum()
    {
        ColorCodeSmartEnum.TryFromValue(_r, out ColorCodeSmartEnum? r);
        ColorCodeSmartEnum.TryFromValue(_b, out ColorCodeSmartEnum? b);
        return (r, b);
    }

    [Benchmark]
    public bool GenEnumValues_Miss()
    {
        return Enums.ColorCode.TryFromValue(_miss, out _);
    }

    [Benchmark]
    public bool Intellenum_Miss()
    {
        return ColorCodeIntellenum.TryFromValue(_miss, out _);
    }

    [Benchmark]
    public bool SmartEnum_Miss()
    {
        return ColorCodeSmartEnum.TryFromValue(_miss, out _);
    }
}
