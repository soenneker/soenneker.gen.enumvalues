using Intellenum;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

/// <summary>
/// Intellenum type matching ColorCode (Red="R", Blue="B") for benchmarking.
/// </summary>
[Intellenum(conversions: Conversions.NewtonsoftJson | Conversions.SystemTextJson, underlyingType: typeof(string))]
[Member("Red", "R")]
[Member("Blue", "B")]
public partial class ColorCodeIntellenum
{
}
