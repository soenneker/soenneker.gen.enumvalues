using Intellenum;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

/// <summary>
/// EnumValue type matching ColorCode (Red="R", Blue="B") for benchmarking.
/// </summary>
[Intellenum<string>]
public partial class ColorCodeIntellenum
{
    public static readonly ColorCodeIntellenum Red = new("R");
    public static readonly ColorCodeIntellenum Blue = new("B");
}