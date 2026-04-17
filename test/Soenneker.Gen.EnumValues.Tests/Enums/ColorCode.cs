namespace Soenneker.Gen.EnumValues.Tests.Enums;

[EnumValue<string>]
public sealed partial class ColorCode
{
    public static readonly ColorCode Red = new("R");
    public static readonly ColorCode Blue = new("B");
}
