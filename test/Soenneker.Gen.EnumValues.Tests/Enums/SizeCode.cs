namespace Soenneker.Gen.EnumValues.Tests.Enums;

[EnumValue<string>]
public readonly partial struct SizeCode
{
    public static readonly SizeCode Small = new("S");
    public static readonly SizeCode Large = new("L");
}
