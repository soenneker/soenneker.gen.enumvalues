namespace Soenneker.Gen.EnumValues.Tests;

[EnumValue<string>]
public sealed partial class ColorCodeLarge
{
    public static readonly ColorCodeLarge Red = new("Red", 1);
    public static readonly ColorCodeLarge Blue = new("Blue", 2);
    public static readonly ColorCodeLarge BabyBlue = new("BabyBlue", 3);
    public static readonly ColorCodeLarge Green = new("Green", 4);
    public static readonly ColorCodeLarge Yellow = new("Yellow", 5);
}
