namespace Soenneker.Gen.EnumValues.Tests;

[EnumValue]
public sealed partial class OrderStatus
{
    public static readonly OrderStatus Pending = new(1, "Pending");
    public static readonly OrderStatus Completed = new(2, "Completed");
}
