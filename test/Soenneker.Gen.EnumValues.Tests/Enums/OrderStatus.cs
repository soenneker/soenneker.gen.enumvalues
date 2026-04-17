namespace Soenneker.Gen.EnumValues.Tests.Enums;

[EnumValue]
public sealed partial class OrderStatus
{
    public static readonly OrderStatus Pending = new(1);
    public static readonly OrderStatus Completed = new(2);
}
