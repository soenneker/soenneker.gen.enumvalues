using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class TryFromNameRoutingBenchmark
{
    private readonly string _red = "Red";
    private readonly string _blue = "Blue";
    private readonly string _miss = "Unknown";
    private readonly string _missingValue = "missing";

    [Benchmark(Baseline = true)]
    public (Enums.ColorCode, Enums.ColorCode) DirectSwitchKnown()
    {
        Enums.ColorCode.TryFromName(_red, out Enums.ColorCode r);
        Enums.ColorCode.TryFromName(_blue, out Enums.ColorCode b);
        return (r, b);
    }

    [Benchmark]
    public (Enums.ColorCode, Enums.ColorCode) PreviousSpanRoutingKnown()
    {
        LegacyTryFromName(_red, out Enums.ColorCode r);
        LegacyTryFromName(_blue, out Enums.ColorCode b);
        return (r, b);
    }

    [Benchmark]
    public bool DirectSwitchMiss() => Enums.ColorCode.TryFromName(_miss, out _);

    [Benchmark]
    public bool PreviousSpanRoutingMiss() => LegacyTryFromName(_miss, out _);

    [Benchmark]
    public bool DirectIsDefinedMiss() => Enums.ColorCode.IsDefined(_missingValue);

    [Benchmark]
    public bool PreviousTryFromIsDefinedMiss() => Enums.ColorCode.TryFromValue(_missingValue, out _);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LegacyTryFromName(string? name, out Enums.ColorCode result)
    {
        if (name is null)
        {
            result = default!;
            return false;
        }

        return LegacyTryFromName(name.AsSpan(), out result);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LegacyTryFromName(ReadOnlySpan<char> name, out Enums.ColorCode result)
    {
        switch (name.Length)
        {
            case 3 when name.SequenceEqual("Red"):
                result = Enums.ColorCode.Red;
                return true;
            case 4 when name.SequenceEqual("Blue"):
                result = Enums.ColorCode.Blue;
                return true;
            default:
                result = default!;
                return false;
        }
    }
}
