using Microsoft.CodeAnalysis;

namespace Soenneker.Gen.EnumValues;

internal sealed class EnumInstance
{
    public EnumInstance(string name, string valueLiteral, string? stringValue, Location location)
    {
        Name = name;
        ValueLiteral = valueLiteral;
        StringValue = stringValue;
        Location = location;
    }

    public string Name { get; }

    public string ValueLiteral { get; }

    public string? StringValue { get; }

    public Location Location { get; }
}
