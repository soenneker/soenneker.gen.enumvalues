namespace Soenneker.Gen.EnumValues.Tests;

[EnumValue]
public readonly partial struct PriorityLevel
{
    public static readonly PriorityLevel Low = new(0, "Low");
    public static readonly PriorityLevel High = new(1, "High");
}
