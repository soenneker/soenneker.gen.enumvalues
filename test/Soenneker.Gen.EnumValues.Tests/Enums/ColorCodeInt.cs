namespace Soenneker.Gen.EnumValues.Tests.Enums;

[EnumValue<int>]
public sealed partial class ColorCodeInt
{
    public static readonly ColorCodeInt Red = new(0);
    public static readonly ColorCodeInt Blue = new(1);
}
