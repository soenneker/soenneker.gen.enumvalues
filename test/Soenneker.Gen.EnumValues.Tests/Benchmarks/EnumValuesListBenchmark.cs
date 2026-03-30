using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class EnumValuesListBenchmark
{
    [Benchmark(Baseline = true)]
    public int GenEnumValues_Small()
    {
        return ColorCode.List.Count;
    }

    [Benchmark]
    public int SmartEnum_Small()
    {
        return ColorCodeSmartEnum.List.Count;
    }

    [Benchmark]
    public int Intellenum_Small()
    {
        return ColorCodeIntellenum.List().Count();
    }

    //[Benchmark]
    //public int GenEnumValues_Large()
    //{
    //    return ColorCodeLarge.List.Count;
    //}
}
