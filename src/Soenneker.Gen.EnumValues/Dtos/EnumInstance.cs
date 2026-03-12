using Microsoft.CodeAnalysis;

namespace Soenneker.Gen.EnumValues.Dtos;

internal sealed class EnumInstance
{
    public EnumInstance(string name, string valueLiteral, string? stringValue, Location location, byte? id = null, string? sourceTypeName = null, string? valueJsonString = null)
    {
        Name = name;
        ValueLiteral = valueLiteral;
        StringValue = stringValue;
        Location = location;
        Id = id;
        SourceTypeName = sourceTypeName;
        ValueJsonString = valueJsonString;
    }

    public string Name { get; }

    public string ValueLiteral { get; }

    public string? StringValue { get; }

    /// <summary>Invariant string form of the value for JSON property names (numeric/bool/Guid etc.).</summary>
    public string? ValueJsonString { get; }

    public Location Location { get; }

    public byte? Id { get; }

    /// <summary>When non-null, this instance was included from another enum-value type (for diagnostics).</summary>
    public string? SourceTypeName { get; }
}
