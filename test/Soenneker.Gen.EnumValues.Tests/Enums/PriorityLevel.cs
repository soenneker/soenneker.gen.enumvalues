namespace Soenneker.Gen.EnumValues.Tests.Enums;

[EnumValue]
public readonly partial struct PriorityLevel
{
    public static readonly PriorityLevel Low = new(0);
    public static readonly PriorityLevel High = new(1);
}
