using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class EnumValuesListBenchmark
{
    [Benchmark(Baseline = true)]
    public System.Collections.Generic.IReadOnlyList<ColorCode> GenEnumValues_Small()
    {
        return ColorCode.List;
    }

   // [Benchmark]
    public System.Collections.Generic.IReadOnlyList<ColorCodeSmartEnum> SmartEnum_Small()
    {
        return ColorCodeSmartEnum.List.ToList();
    }

    [Benchmark]
    public System.Collections.Generic.IEnumerable<ColorCodeIntellenum> Intellenum_Small()
    {
        return ColorCodeIntellenum.List();
    }

    //[Benchmark]
    public System.Collections.Generic.IReadOnlyList<ColorCodeLarge> GenEnumValues_Large()
    {
        return ColorCodeLarge.List;
    }
}
