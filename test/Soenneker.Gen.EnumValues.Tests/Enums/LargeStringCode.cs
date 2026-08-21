namespace Soenneker.Gen.EnumValues.Tests.Enums;

[EnumValue<string>]
public sealed partial class LargeStringCode
{
    public static readonly LargeStringCode Alpha = new("alpha");
    public static readonly LargeStringCode Bravo = new("bravo");
    public static readonly LargeStringCode Charlie = new("charlie");
    public static readonly LargeStringCode Delta = new("delta");
    public static readonly LargeStringCode Echo = new("echo");
    public static readonly LargeStringCode Foxtrot = new("foxtrot");
    public static readonly LargeStringCode Golf = new("golf");
    public static readonly LargeStringCode Hotel = new("hotel");
    public static readonly LargeStringCode Quoted = new("quoted\"value");
    public static readonly LargeStringCode Unicode = new("éclair");
}
