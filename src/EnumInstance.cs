using Microsoft.CodeAnalysis;

namespace Soenneker.Gen.EnumValues;

internal sealed class EnumInstance
{
    public EnumInstance(string name, string valueLiteral, string? stringValue, Location location, byte? id = null, string? sourceTypeName = null)
    {
        Name = name;
        ValueLiteral = valueLiteral;
        StringValue = stringValue;
        Location = location;
        Id = id;
        SourceTypeName = sourceTypeName;
    }

    public string Name { get; }

    public string ValueLiteral { get; }

    public string? StringValue { get; }

    public Location Location { get; }

    public byte? Id { get; }

    /// <summary>When non-null, this instance was included from another enum-value type (for diagnostics).</summary>
    public string? SourceTypeName { get; }
}
