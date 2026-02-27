namespace Soenneker.Gen.EnumValues.Tests;

/// <summary>
/// Composed type: own instances (None, Inset) plus all values from GlobalKeyword (Initial, Inherit, Unset).
/// </summary>
[EnumValue<string>]
[IncludeEnumValues(typeof(GlobalKeyword))]
public sealed partial class BoxShadowKeyword
{
    public static readonly BoxShadowKeyword None = new("none", 1);
    public static readonly BoxShadowKeyword Inset = new("inset", 2);
}
