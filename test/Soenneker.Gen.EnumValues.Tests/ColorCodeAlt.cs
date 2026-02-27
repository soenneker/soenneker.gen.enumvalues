namespace Soenneker.Gen.EnumValues.Tests;

[EnumValue<string>]
public sealed partial class ColorCodeAlt
{
    public static readonly ColorCodeAlt Red = new("R", 1);
    public static readonly ColorCodeAlt Blue = new("B", 2);
    public static readonly ColorCodeAlt BabyBlue = new("BB", 3);
}
