namespace Soenneker.Gen.EnumValues.Tests;

[EnumValue<string>]
public sealed partial class ColorCodeNameOf
{
    public static readonly ColorCodeNameOf Red = new("Red", 1);
    public static readonly ColorCodeNameOf Blue = new("Blue", 2);
}
