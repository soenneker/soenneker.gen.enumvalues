using Intellenum;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

/// <summary>
/// EnumValue type matching ColorCode (Red="R", Blue="B") for benchmarking.
/// </summary>
[Intellenum<int>]
public partial class ColorCodeIntIntellenum
{
    public static readonly ColorCodeIntIntellenum Red = new(0);
    public static readonly ColorCodeIntIntellenum Blue = new(1);
}
