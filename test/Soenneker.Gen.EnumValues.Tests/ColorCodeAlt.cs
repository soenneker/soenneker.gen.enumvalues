namespace Soenneker.Gen.EnumValues.Tests;

[EnumValue<string>]
public sealed partial class ColorCodeAlt
{
    public static readonly ColorCodeAlt Red = new("R");
    public static readonly ColorCodeAlt Blue = new("B");
    public static readonly ColorCodeAlt BabyBlue = new("BB");
}
