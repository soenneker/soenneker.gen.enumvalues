using System;
using AwesomeAssertions;
using System.Text.Json;
using Xunit;

namespace Soenneker.Gen.EnumValues.Tests;

public sealed class EnumValuesGeneratorTests
{
    public EnumValuesGeneratorTests(ITestOutputHelper output)
    {
    }

    [Fact]
    public void Int_default_attribute_generates_fast_lookups()
    {
        OrderStatus.TryFromValue(1, out OrderStatus? pending).Should().BeTrue();
        pending.Should().BeSameAs(OrderStatus.Pending);

        OrderStatus completed = OrderStatus.FromValue(2);
        completed.Should().BeSameAs(OrderStatus.Completed);

        OrderStatus.TryFromName("Pending", out OrderStatus? fromName).Should().BeTrue();
        fromName.Should().BeSameAs(OrderStatus.Pending);

        OrderStatus.List.Count.Should().Be(2);
    }

    [Fact]
    public void Generic_string_attribute_generates_fast_lookups()
    {
        ColorCode.TryFromValue("R", out ColorCode? red).Should().BeTrue();
        red.Should().BeSameAs(ColorCode.Red);
        ColorCode.TryFromValue("R".AsSpan(), out ColorCode? redFromSpan).Should().BeTrue();
        redFromSpan.Should().BeSameAs(ColorCode.Red);

        ColorCode blue = ColorCode.FromName("Blue");
        blue.Should().BeSameAs(ColorCode.Blue);

        ColorCode.TryFromName("Red", out ColorCode? fromName).Should().BeTrue();
        fromName.Should().BeSameAs(ColorCode.Red);

        ColorCode.List.Count.Should().Be(2);
    }

    [Fact]
    public void Json_round_trips_enum_values()
    {
        string intJson = JsonSerializer.Serialize(OrderStatus.Pending);
        intJson.Should().Be("1");

        var intValue = JsonSerializer.Deserialize<OrderStatus>("1");
        intValue.Should().BeSameAs(OrderStatus.Pending);

        string stringJson = JsonSerializer.Serialize(ColorCode.Red);
        stringJson.Should().Be("\"R\"");

        var stringValue = JsonSerializer.Deserialize<ColorCode>("\"R\"");
        stringValue.Should().BeSameAs(ColorCode.Red);
    }

    [Fact]
    public void Json_throws_on_unknown_value()
    {
        Action act1 = () => JsonSerializer.Deserialize<OrderStatus>("999");
        act1.Should().Throw<JsonException>();

        Action act2 = () => JsonSerializer.Deserialize<ColorCode>("\"Z\"");
        act2.Should().Throw<JsonException>();
    }

    [Fact]
    public void Struct_int_default_attribute_generates_fast_lookups()
    {
        PriorityLevel.TryFromValue(0, out PriorityLevel low).Should().BeTrue();
        low.Should().Be(PriorityLevel.Low);

        PriorityLevel high = PriorityLevel.FromValue(1);
        high.Should().Be(PriorityLevel.High);

        PriorityLevel.TryFromName("Low", out PriorityLevel fromName).Should().BeTrue();
        fromName.Should().Be(PriorityLevel.Low);

        PriorityLevel.List.Count.Should().Be(2);
    }

    [Fact]
    public void Struct_generic_string_attribute_generates_fast_lookups()
    {
        SizeCode.TryFromValue("S", out SizeCode small).Should().BeTrue();
        small.Should().Be(SizeCode.Small);
        SizeCode.TryFromValue("S".AsSpan(), out SizeCode smallFromSpan).Should().BeTrue();
        smallFromSpan.Should().Be(SizeCode.Small);

        SizeCode large = SizeCode.FromName("Large");
        large.Should().Be(SizeCode.Large);

        SizeCode.TryFromName("Small", out SizeCode fromName).Should().BeTrue();
        fromName.Should().Be(SizeCode.Small);

        SizeCode.List.Count.Should().Be(2);
    }

    [Fact]
    public void Struct_Json_round_trips_enum_values()
    {
        string intJson = JsonSerializer.Serialize(PriorityLevel.Low);
        intJson.Should().Be("0");

        var intValue = JsonSerializer.Deserialize<PriorityLevel>("0");
        intValue.Should().Be(PriorityLevel.Low);

        string stringJson = JsonSerializer.Serialize(SizeCode.Small);
        stringJson.Should().Be("\"S\"");

        var stringValue = JsonSerializer.Deserialize<SizeCode>("\"S\"");
        stringValue.Should().Be(SizeCode.Small);
    }

    [Fact]
    public void Struct_Json_throws_on_unknown_value()
    {
        Action act1 = () => JsonSerializer.Deserialize<PriorityLevel>("999");
        act1.Should().Throw<JsonException>();

        Action act2 = () => JsonSerializer.Deserialize<SizeCode>("\"Z\"");
        act2.Should().Throw<JsonException>();
    }

    [Fact]
    public void Value_constants_can_be_used_for_switch_labels()
    {
        const string colorCode = ColorCode.RedValue;
        const int statusCode = OrderStatus.PendingValue;

        var matchedColor = false;
        switch (colorCode)
        {
            case ColorCode.RedValue:
                matchedColor = true;
                break;
        }

        var matchedStatus = false;
        switch (statusCode)
        {
            case OrderStatus.PendingValue:
                matchedStatus = true;
                break;
        }

        matchedColor.Should().BeTrue();
        matchedStatus.Should().BeTrue();
    }

    [Fact]
    public void Newtonsoft_json_round_trips_enum_values()
    {
        string intJson = global::Newtonsoft.Json.JsonConvert.SerializeObject(OrderStatus.Pending);
        intJson.Should().Be("1");

        var intValue = global::Newtonsoft.Json.JsonConvert.DeserializeObject<OrderStatus>("1");
        intValue.Should().BeSameAs(OrderStatus.Pending);

        string stringJson = global::Newtonsoft.Json.JsonConvert.SerializeObject(ColorCode.Red);
        stringJson.Should().Be("\"R\"");

        var stringValue = global::Newtonsoft.Json.JsonConvert.DeserializeObject<ColorCode>("\"R\"");
        stringValue.Should().BeSameAs(ColorCode.Red);
    }

    [Fact]
    public void Name_is_generated_for_class_enum_values()
    {
        ColorCode.Red.Name.Should().Be("Red");
        ColorCode.Blue.Name.Should().Be("Blue");

        OrderStatus.Pending.Name.Should().Be("Pending");
        OrderStatus.Completed.Name.Should().Be("Completed");
    }

    [Fact]
    public void Name_is_generated_for_class_enum_instances()
    {
        ColorCode variable = ColorCode.Red;
        variable.Name.Should()
                .Be("Red");
    }

    [Fact]
    public void Name_is_generated_for_class_enum_on_objects()
    {
        var testObject = new TestObject();
        testObject.ColorCode = ColorCode.Red;

        testObject.ColorCode.Name.Should()
                  .Be("Red");
    }

    [Fact]
    public void Name_is_generated_for_struct_enum_values()
    {
        SizeCode.Small.Name.Should().Be("Small");
        SizeCode.Large.Name.Should().Be("Large");

        PriorityLevel.Low.Name.Should().Be("Low");
        PriorityLevel.High.Name.Should().Be("High");
    }

    [Fact]
    public void Name_returns_empty_string_for_default_struct()
    {
        default(PriorityLevel).Name.Should().Be("");
        default(SizeCode).Name.Should().Be("");
    }

    [Fact]
    public void All_enum_value_instances_have_Name_property_set()
    {
        foreach (ColorCode instance in ColorCode.List)
            instance.Name.Should().NotBeNullOrEmpty();

        foreach (OrderStatus instance in OrderStatus.List)
            instance.Name.Should().NotBeNullOrEmpty();

        foreach (SizeCode instance in SizeCode.List)
            instance.Name.Should().NotBeNullOrEmpty();

        foreach (PriorityLevel instance in PriorityLevel.List)
            instance.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Name_property_matches_nameof_for_enum_value_members()
    {
        nameof(ColorCode.Red).Should().Be(ColorCode.Red.Name);
        nameof(ColorCode.Blue).Should().Be(ColorCode.Blue.Name);

        nameof(OrderStatus.Pending).Should().Be(OrderStatus.Pending.Name);
        nameof(OrderStatus.Completed).Should().Be(OrderStatus.Completed.Name);

        nameof(SizeCode.Small).Should().Be(SizeCode.Small.Name);
        nameof(SizeCode.Large).Should().Be(SizeCode.Large.Name);

        nameof(PriorityLevel.Low).Should().Be(PriorityLevel.Low.Name);
        nameof(PriorityLevel.High).Should().Be(PriorityLevel.High.Name);
    }

    // --- EnumValue<string>: implicit conversion and string equality ---

    [Fact]
    public void String_enum_implicit_conversion_to_string_works()
    {
        string red = ColorCode.Red;
        red.Should().Be("R");

        string blue = ColorCode.Blue;
        blue.Should().Be("B");
    }

    [Fact]
    public void String_Equals_accepts_enum_via_implicit_conversion()
    {
        string.Equals("R", ColorCode.Red, StringComparison.Ordinal).Should().BeTrue();
        string.Equals("R", ColorCode.Red, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        string.Equals("r", ColorCode.Red, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        string.Equals("B", ColorCode.Red, StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void String_enum_equality_with_string_both_directions()
    {
        (ColorCode.Red == "R").Should().BeTrue();
        (ColorCode.Red != "R").Should().BeFalse();
        (ColorCode.Red == "B").Should().BeFalse();
        (ColorCode.Red != "B").Should().BeTrue();

        ("R" == ColorCode.Red).Should().BeTrue();
        ("R" != ColorCode.Red).Should().BeFalse();
        ("B" == ColorCode.Red).Should().BeFalse();
        ("B" != ColorCode.Red).Should().BeTrue();
    }

    [Fact]
    public void String_enum_equality_with_null_string()
    {
        string? n = null;
        (ColorCode.Red == n).Should().BeFalse();
        (n == ColorCode.Red).Should().BeFalse();
        (ColorCode.Red != n).Should().BeTrue();
        (n != ColorCode.Red).Should().BeTrue();
    }

    [Fact]
    public void Singleton_equals_should_be_true()
    {
        var testObject1 = new TestObject { ColorCode = ColorCode.Red };
        var testObject2 = new TestObject { ColorCode = ColorCode.Red };

       (testObject1.ColorCode == testObject2.ColorCode).Should()
                                                        .BeTrue();
    }

    [Fact]
    public void String_enum_Equals_and_GetHashCode_by_value()
    {
        ColorCode red1 = ColorCode.FromValue("R");
        ColorCode red2 = ColorCode.Red;
        red1.Should().Be(red2);
        red1.GetHashCode().Should().Be(red2.GetHashCode());
        red1.Equals((object)red2).Should().BeTrue();
    }

    // --- EnumValue<int>: IEquatable, ==/!= with int, ToString, explicit conversion ---

    [Fact]
    public void Int_enum_implements_IEquatable_TSelf_class()
    {
        OrderStatus pending = OrderStatus.Pending;
        OrderStatus completed = OrderStatus.Completed;
        pending.Equals(OrderStatus.Pending).Should().BeTrue();
        pending.Equals(OrderStatus.Completed).Should().BeFalse();
        completed.Equals(OrderStatus.Completed).Should().BeTrue();
    }

    [Fact]
    public void Int_enum_implements_IEquatable_TSelf_struct()
    {
        PriorityLevel low = PriorityLevel.Low;
        PriorityLevel high = PriorityLevel.High;
        low.Equals(PriorityLevel.Low).Should().BeTrue();
        low.Equals(PriorityLevel.High).Should().BeFalse();
        high.Equals(PriorityLevel.High).Should().BeTrue();
    }

    [Fact]
    public void Int_enum_implements_IEquatable_int()
    {
        OrderStatus.Pending.Equals(1).Should().BeTrue();
        OrderStatus.Pending.Equals(2).Should().BeFalse();
        OrderStatus.Completed.Equals(2).Should().BeTrue();

        PriorityLevel.Low.Equals(0).Should().BeTrue();
        PriorityLevel.Low.Equals(1).Should().BeFalse();
        PriorityLevel.High.Equals(1).Should().BeTrue();
    }

    [Fact]
    public void Int_enum_equality_TSelf_TSelf()
    {
        OrderStatus pending = OrderStatus.Pending;
        OrderStatus completed = OrderStatus.Completed;
        (pending == OrderStatus.Pending).Should().BeTrue();
        (pending != completed).Should().BeTrue();
        (pending == completed).Should().BeFalse();

        PriorityLevel low = PriorityLevel.Low;
        PriorityLevel high = PriorityLevel.High;
        (low == PriorityLevel.Low).Should().BeTrue();
        (low != high).Should().BeTrue();
        (low == high).Should().BeFalse();
    }

    [Fact]
    public void Int_enum_equality_TSelf_int_both_directions()
    {
        (OrderStatus.Pending == 1).Should().BeTrue();
        (OrderStatus.Pending != 1).Should().BeFalse();
        (OrderStatus.Pending == 2).Should().BeFalse();
        (1 == OrderStatus.Pending).Should().BeTrue();
        (2 != OrderStatus.Pending).Should().BeTrue();

        (PriorityLevel.Low == 0).Should().BeTrue();
        (PriorityLevel.High == 1).Should().BeTrue();
        (0 == PriorityLevel.Low).Should().BeTrue();
        (1 == PriorityLevel.High).Should().BeTrue();
    }

    [Fact]
    public void Int_enum_ToString_uses_invariant_culture()
    {
        OrderStatus.Pending.ToString().Should().Be("1");
        OrderStatus.Completed.ToString().Should().Be("2");
        PriorityLevel.Low.ToString().Should().Be("0");
        PriorityLevel.High.ToString().Should().Be("1");
    }

    [Fact]
    public void Int_enum_explicit_conversion_to_int()
    {
        ((int)OrderStatus.Pending).Should().Be(1);
        ((int)OrderStatus.Completed).Should().Be(2);
        ((int)PriorityLevel.Low).Should().Be(0);
        ((int)PriorityLevel.High).Should().Be(1);
    }

    [Fact]
    public void Int_enum_override_Equals_object_and_GetHashCode()
    {
        OrderStatus pending = OrderStatus.FromValue(1);
        pending.Equals((object)OrderStatus.Pending).Should().BeTrue();
        pending.GetHashCode().Should().Be(OrderStatus.Pending.GetHashCode());

        PriorityLevel low = PriorityLevel.FromValue(0);
        low.Equals((object)PriorityLevel.Low).Should().BeTrue();
        low.GetHashCode().Should().Be(PriorityLevel.Low.GetHashCode());
    }
}
