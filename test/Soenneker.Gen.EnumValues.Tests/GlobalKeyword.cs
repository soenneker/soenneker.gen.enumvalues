namespace Soenneker.Gen.EnumValues.Tests;

/// <summary>
/// Source enum-value type used by BoxShadowKeyword via [IncludeEnumValues].
/// </summary>
[EnumValue<string>]
public sealed partial class GlobalKeyword
{
    public static readonly GlobalKeyword Initial = new("initial", 1);
    public static readonly GlobalKeyword Inherit = new("inherit", 2);
    public static readonly GlobalKeyword Unset = new("unset", 3);
}
