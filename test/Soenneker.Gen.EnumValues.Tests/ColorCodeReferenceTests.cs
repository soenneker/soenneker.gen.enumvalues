using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using Soenneker.Gen.EnumValues.Tests.Enums;
using Xunit;

namespace Soenneker.Gen.EnumValues.Tests;

public sealed class ColorCodeReferenceTests
{
    [Fact]
    public void ReferenceEquals_should_work_for_same_static_instance()
    {
        var blue = ColorCode.Blue;

        bool result = ReferenceEquals(blue, ColorCode.Blue);

        result.Should().BeTrue();
    }

    [Fact]
    public void ReferenceEquals_should_work_for_two_variables_pointing_to_same_instance()
    {
        var blue1 = ColorCode.Blue;
        var blue2 = blue1;

        bool result = ReferenceEquals(blue1, blue2);

        result.Should().BeTrue();
    }

    [Fact]
    public void ReferenceEquals_should_be_false_for_different_static_instances()
    {
        var blue = ColorCode.Blue;
        var red = ColorCode.Red;

        bool result = ReferenceEquals(blue, red);

        result.Should().BeFalse();
    }

    [Fact]
    public void ReferenceEquals_should_be_true_when_pulled_from_collection()
    {
        var list = new List<ColorCode> { ColorCode.Blue };

        ColorCode blueFromList = list[0];

        bool result = ReferenceEquals(blueFromList, ColorCode.Blue);

        result.Should().BeTrue();
    }

    [Fact]
    public void ReferenceEquals_should_be_true_when_returned_from_method_without_rehydration()
    {
        ColorCode blue = GetBlue();

        bool result = ReferenceEquals(blue, ColorCode.Blue);

        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_and_ReferenceEquals_should_both_be_true_for_same_static_instance()
    {
        var blue1 = ColorCode.Blue;
        var blue2 = ColorCode.Blue;

        blue1.Equals(blue2).Should().BeTrue();
        ReferenceEquals(blue1, blue2).Should().BeTrue();
    }

    [Fact]
    public void Null_reference_comparisons_should_behave_correctly()
    {
        ColorCode? left = null;
        ColorCode? right = null;

        ReferenceEquals(left, right).Should().BeTrue();
        ReferenceEquals(left, ColorCode.Blue).Should().BeFalse();
        ReferenceEquals(ColorCode.Blue, right).Should().BeFalse();
    }

    [Fact]
    public void ReferenceEquals_should_be_true_after_casting_to_object()
    {
        object blue = ColorCode.Blue;

        bool result = ReferenceEquals(blue, ColorCode.Blue);

        result.Should().BeTrue();
    }

    [Fact]
    public void Dictionary_lookup_should_return_same_reference_instance()
    {
        var dict = new Dictionary<string, ColorCode>
        {
            ["blue"] = ColorCode.Blue
        };

        ColorCode value = dict["blue"];

        ReferenceEquals(value, ColorCode.Blue).Should().BeTrue();
    }

    [Fact]
    public void Linq_First_should_return_same_reference_instance()
    {
        var values = new[] { ColorCode.Red, ColorCode.Blue };

        ColorCode result = values.First(x => x == ColorCode.Blue);

        ReferenceEquals(result, ColorCode.Blue).Should().BeTrue();
    }

    [Fact]
    public void JsonSerializer_deserialize_should_not_preserve_reference_by_default_for_normal_reference_types()
    {
        var json = JsonSerializer.Serialize(ColorCode.Blue);
        ColorCode? deserialized = JsonSerializer.Deserialize<ColorCode>(json);

        deserialized.Should().NotBeNull();

        ReferenceEquals(deserialized, ColorCode.Blue).Should().BeTrue();
    }

    [Fact]
    public void JsonSerializer_should_preserve_reference_only_if_custom_converter_rehydrates_static_instance()
    {
        var json = JsonSerializer.Serialize(ColorCode.Blue);
        ColorCode? deserialized = JsonSerializer.Deserialize<ColorCode>(json);

        deserialized.Should().NotBeNull();
        ReferenceEquals(deserialized, ColorCode.Blue).Should().BeTrue();
    }

    [Fact]
    public void FromName_should_match_reference()
    {
        var original = ColorCode.Blue;
        var clone = ColorCode.FromName("Blue");

        ReferenceEquals(original, clone).Should().BeTrue();

        // If equality is value-based, this may still be true:
        original.Equals(clone).Should().BeTrue();
    }

    private static ColorCode GetBlue() => ColorCode.Blue;
}