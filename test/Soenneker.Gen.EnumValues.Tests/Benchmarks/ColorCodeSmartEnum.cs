using Ardalis.SmartEnum;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

/// <summary>
/// SmartEnum type matching ColorCode (Red="R", Blue="B") for benchmarking.
/// </summary>
public sealed class ColorCodeSmartEnum : SmartEnum<ColorCodeSmartEnum, string>
{
    public static readonly ColorCodeSmartEnum Red = new(nameof(Red), "R");
    public static readonly ColorCodeSmartEnum Blue = new(nameof(Blue), "B");

    private ColorCodeSmartEnum(string name, string value) : base(name, value)
    {
    }
}
