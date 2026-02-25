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

        bool matchedColor = false;
        switch (colorCode)
        {
            case ColorCode.RedValue:
                matchedColor = true;
                break;
        }

        bool matchedStatus = false;
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

        OrderStatus? intValue = global::Newtonsoft.Json.JsonConvert.DeserializeObject<OrderStatus>("1");
        intValue.Should().BeSameAs(OrderStatus.Pending);

        string stringJson = global::Newtonsoft.Json.JsonConvert.SerializeObject(ColorCode.Red);
        stringJson.Should().Be("\"R\"");

        ColorCode? stringValue = global::Newtonsoft.Json.JsonConvert.DeserializeObject<ColorCode>("\"R\"");
        stringValue.Should().BeSameAs(ColorCode.Red);
    }
}
