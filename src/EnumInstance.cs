using Microsoft.CodeAnalysis;

namespace Soenneker.Gen.EnumValues;

internal sealed class EnumInstance
{
    public EnumInstance(string name, string valueLiteral, Location location)
    {
        Name = name;
        ValueLiteral = valueLiteral;
        Location = location;
    }

    public string Name { get; }

    public string ValueLiteral { get; }

    public Location Location { get; }
}
