namespace Soenneker.Gen.EnumValues.Tests;

[EnumValue<string>]
public readonly partial struct SizeCode
{
    public static readonly SizeCode Small = new("S", 1);
    public static readonly SizeCode Large = new("L", 2);
}
