using System;
using AwesomeAssertions;
using System.Text.Json;

namespace Soenneker.Gen.EnumValues.Tests;

public sealed class EnumValuesGeneratorTests
{
    public EnumValuesGeneratorTests()
    {
    }

    [Test]
    public void Int_default_attribute_generates_fast_lookups()
    {
        Enums.OrderStatus.TryFromValue(1, out Enums.OrderStatus? pending).Should().BeTrue();
        pending.Should().BeSameAs(Enums.OrderStatus.Pending);

        Enums.OrderStatus completed = Enums.OrderStatus.FromValue(2);
        completed.Should().BeSameAs(Enums.OrderStatus.Completed);

        Enums.OrderStatus.TryFromName("Pending", out Enums.OrderStatus? fromName).Should().BeTrue();
        fromName.Should().BeSameAs(Enums.OrderStatus.Pending);

        Enums.OrderStatus.List.Count.Should().Be(2);
    }

    [Test]
    public void Generic_string_attribute_generates_fast_lookups()
    {
        Enums.ColorCode.TryFromValue("R", out Enums.ColorCode? red).Should().BeTrue();
        red.Should().BeSameAs(Enums.ColorCode.Red);
        Enums.ColorCode.TryFromValue("R".AsSpan(), out Enums.ColorCode? redFromSpan).Should().BeTrue();
        redFromSpan.Should().BeSameAs(Enums.ColorCode.Red);

        Enums.ColorCode blue = Enums.ColorCode.FromName("Blue");
        blue.Should().BeSameAs(Enums.ColorCode.Blue);

        Enums.ColorCode.TryFromName("Red", out Enums.ColorCode? fromName).Should().BeTrue();
        fromName.Should().BeSameAs(Enums.ColorCode.Red);

        Enums.ColorCode.List.Count.Should().Be(2);
    }

    [Test]
    public void Json_round_trips_enum_values()
    {
        string intJson = JsonSerializer.Serialize(Enums.OrderStatus.Pending);
        intJson.Should().Be("1");

        var intValue = JsonSerializer.Deserialize<Enums.OrderStatus>("1");
        intValue.Should().BeSameAs(Enums.OrderStatus.Pending);

        string stringJson = JsonSerializer.Serialize(Enums.ColorCode.Red);
        stringJson.Should().Be("\"R\"");

        var stringValue = JsonSerializer.Deserialize<Enums.ColorCode>("\"R\"");
        stringValue.Should().BeSameAs(Enums.ColorCode.Red);
    }

    [Test]
    public void Json_throws_on_unknown_value()
    {
        Action act1 = () => JsonSerializer.Deserialize<Enums.OrderStatus>("999");
        act1.Should().Throw<JsonException>();

        Action act2 = () => JsonSerializer.Deserialize<Enums.ColorCode>("\"Z\"");
        act2.Should().Throw<JsonException>();
    }

    [Test]
    public void Struct_int_default_attribute_generates_fast_lookups()
    {
        Enums.PriorityLevel.TryFromValue(0, out Enums.PriorityLevel low).Should().BeTrue();
        low.Should().Be(Enums.PriorityLevel.Low);

        Enums.PriorityLevel high = Enums.PriorityLevel.FromValue(1);
        high.Should().Be(Enums.PriorityLevel.High);

        Enums.PriorityLevel.TryFromName("Low", out Enums.PriorityLevel fromName).Should().BeTrue();
        fromName.Should().Be(Enums.PriorityLevel.Low);

        Enums.PriorityLevel.List.Count.Should().Be(2);
    }

    [Test]
    public void Struct_generic_string_attribute_generates_fast_lookups()
    {
        Enums.SizeCode.TryFromValue("S", out Enums.SizeCode small).Should().BeTrue();
        small.Should().Be(Enums.SizeCode.Small);
        Enums.SizeCode.TryFromValue("S".AsSpan(), out Enums.SizeCode smallFromSpan).Should().BeTrue();
        smallFromSpan.Should().Be(Enums.SizeCode.Small);

        Enums.SizeCode large = Enums.SizeCode.FromName("Large");
        large.Should().Be(Enums.SizeCode.Large);

        Enums.SizeCode.TryFromName("Small", out Enums.SizeCode fromName).Should().BeTrue();
        fromName.Should().Be(Enums.SizeCode.Small);

        Enums.SizeCode.List.Count.Should().Be(2);
    }

    [Test]
    public void Struct_Json_round_trips_enum_values()
    {
        string intJson = JsonSerializer.Serialize(Enums.PriorityLevel.Low);
        intJson.Should().Be("0");

        var intValue = JsonSerializer.Deserialize<Enums.PriorityLevel>("0");
        intValue.Should().Be(Enums.PriorityLevel.Low);

        string stringJson = JsonSerializer.Serialize(Enums.SizeCode.Small);
        stringJson.Should().Be("\"S\"");

        var stringValue = JsonSerializer.Deserialize<Enums.SizeCode>("\"S\"");
        stringValue.Should().Be(Enums.SizeCode.Small);
    }

    [Test]
    public void Struct_Json_throws_on_unknown_value()
    {
        Action act1 = () => JsonSerializer.Deserialize<Enums.PriorityLevel>("999");
        act1.Should().Throw<JsonException>();

        Action act2 = () => JsonSerializer.Deserialize<Enums.SizeCode>("\"Z\"");
        act2.Should().Throw<JsonException>();
    }

    [Test]
    public void Value_constants_can_be_used_for_switch_labels()
    {
        const string colorCode = Enums.ColorCode.RedValue;
        const int statusCode = Enums.OrderStatus.PendingValue;

        var matchedColor = false;
        switch (colorCode)
        {
            case Enums.ColorCode.RedValue:
                matchedColor = true;
                break;
        }

        var matchedStatus = false;
        switch (statusCode)
        {
            case Enums.OrderStatus.PendingValue:
                matchedStatus = true;
                break;
        }

        matchedColor.Should().BeTrue();
        matchedStatus.Should().BeTrue();
    }

    [Test]
    public void Newtonsoft_json_round_trips_enum_values()
    {
        string intJson = global::Newtonsoft.Json.JsonConvert.SerializeObject(Enums.OrderStatus.Pending);
        intJson.Should().Be("1");

        var intValue = global::Newtonsoft.Json.JsonConvert.DeserializeObject<Enums.OrderStatus>("1");
        intValue.Should().BeSameAs(Enums.OrderStatus.Pending);

        string stringJson = global::Newtonsoft.Json.JsonConvert.SerializeObject(Enums.ColorCode.Red);
        stringJson.Should().Be("\"R\"");

        var stringValue = global::Newtonsoft.Json.JsonConvert.DeserializeObject<Enums.ColorCode>("\"R\"");
        stringValue.Should().BeSameAs(Enums.ColorCode.Red);
    }

    [Test]
    public void Name_is_generated_for_class_enum_values()
    {
        Enums.ColorCode.Red.Name.Should().Be("Red");
        Enums.ColorCode.Blue.Name.Should().Be("Blue");

        Enums.OrderStatus.Pending.Name.Should().Be("Pending");
        Enums.OrderStatus.Completed.Name.Should().Be("Completed");
    }

    [Test]
    public void Name_is_generated_for_class_enum_instances()
    {
        Enums.ColorCode variable = Enums.ColorCode.Red;
        variable.Name.Should()
                .Be("Red");
    }

    [Test]
    public void Name_is_generated_for_class_enum_on_objects()
    {
        var testObject = new TestObject();
        testObject.ColorCode = Enums.ColorCode.Red;

        testObject.ColorCode.Name.Should()
                  .Be("Red");
    }

    [Test]
    public void Name_is_generated_for_struct_enum_values()
    {
        Enums.SizeCode.Small.Name.Should().Be("Small");
        Enums.SizeCode.Large.Name.Should().Be("Large");

        Enums.PriorityLevel.Low.Name.Should().Be("Low");
        Enums.PriorityLevel.High.Name.Should().Be("High");
    }

    [Test]
    public void Name_returns_empty_string_for_default_struct()
    {
        default(Enums.PriorityLevel).Name.Should().Be("Low");
        default(Enums.SizeCode).Name.Should().Be("");
    }

    [Test]
    public void All_enum_value_instances_have_Name_property_set()
    {
        foreach (Enums.ColorCode instance in Enums.ColorCode.List)
            instance.Name.Should().NotBeNullOrEmpty();

        foreach (Enums.OrderStatus instance in Enums.OrderStatus.List)
            instance.Name.Should().NotBeNullOrEmpty();

        foreach (Enums.SizeCode instance in Enums.SizeCode.List)
            instance.Name.Should().NotBeNullOrEmpty();

        foreach (Enums.PriorityLevel instance in Enums.PriorityLevel.List)
            instance.Name.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Name_property_matches_nameof_for_enum_value_members()
    {
        nameof(Enums.ColorCode.Red).Should().Be(Enums.ColorCode.Red.Name);
        nameof(Enums.ColorCode.Blue).Should().Be(Enums.ColorCode.Blue.Name);

        nameof(Enums.OrderStatus.Pending).Should().Be(Enums.OrderStatus.Pending.Name);
        nameof(Enums.OrderStatus.Completed).Should().Be(Enums.OrderStatus.Completed.Name);

        nameof(Enums.SizeCode.Small).Should().Be(Enums.SizeCode.Small.Name);
        nameof(Enums.SizeCode.Large).Should().Be(Enums.SizeCode.Large.Name);

        nameof(Enums.PriorityLevel.Low).Should().Be(Enums.PriorityLevel.Low.Name);
        nameof(Enums.PriorityLevel.High).Should().Be(Enums.PriorityLevel.High.Name);
    }

    // --- EnumValue<string>: implicit conversion and string equality ---

    [Test]
    public void String_enum_implicit_conversion_to_string_works()
    {
        string red = Enums.ColorCode.Red;
        red.Should().Be("R");

        string blue = Enums.ColorCode.Blue;
        blue.Should().Be("B");
    }

    [Test]
    public void String_Equals_accepts_enum_via_implicit_conversion()
    {
        string.Equals("R", Enums.ColorCode.Red, StringComparison.Ordinal).Should().BeTrue();
        string.Equals("R", Enums.ColorCode.Red, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        string.Equals("r", Enums.ColorCode.Red, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        string.Equals("B", Enums.ColorCode.Red, StringComparison.Ordinal).Should().BeFalse();
    }

    [Test]
    public void String_enum_equality_with_string_both_directions()
    {
        (Enums.ColorCode.Red == "R").Should().BeTrue();
        (Enums.ColorCode.Red != "R").Should().BeFalse();
        (Enums.ColorCode.Red == "B").Should().BeFalse();
        (Enums.ColorCode.Red != "B").Should().BeTrue();

        ("R" == Enums.ColorCode.Red).Should().BeTrue();
        ("R" != Enums.ColorCode.Red).Should().BeFalse();
        ("B" == Enums.ColorCode.Red).Should().BeFalse();
        ("B" != Enums.ColorCode.Red).Should().BeTrue();
    }

    [Test]
    public void String_enum_equality_with_null_string()
    {
        string? n = null;
        (Enums.ColorCode.Red == n).Should().BeFalse();
        (n == Enums.ColorCode.Red).Should().BeFalse();
        (Enums.ColorCode.Red != n).Should().BeTrue();
        (n != Enums.ColorCode.Red).Should().BeTrue();
    }

    [Test]
    public void Singleton_equals_should_be_true()
    {
        var testObject1 = new TestObject { ColorCode = Enums.ColorCode.Red };
        var testObject2 = new TestObject { ColorCode = Enums.ColorCode.Red };

       (testObject1.ColorCode == testObject2.ColorCode).Should()
                                                        .BeTrue();
    }

    [Test]
    public void String_enum_Equals_and_GetHashCode_by_value()
    {
        Enums.ColorCode red1 = Enums.ColorCode.FromValue("R");
        Enums.ColorCode red2 = Enums.ColorCode.Red;
        red1.Should().Be(red2);
        red1.GetHashCode().Should().Be(red2.GetHashCode());
        red1.Equals((object)red2).Should().BeTrue();
    }

    // --- EnumValue<int>: IEquatable, ==/!= with int, ToString, explicit conversion ---

    [Test]
    public void Int_enum_implements_IEquatable_TSelf_class()
    {
        Enums.OrderStatus pending = Enums.OrderStatus.Pending;
        Enums.OrderStatus completed = Enums.OrderStatus.Completed;
        pending.Equals(Enums.OrderStatus.Pending).Should().BeTrue();
        pending.Equals(Enums.OrderStatus.Completed).Should().BeFalse();
        completed.Equals(Enums.OrderStatus.Completed).Should().BeTrue();
    }

    [Test]
    public void Int_enum_implements_IEquatable_TSelf_struct()
    {
        Enums.PriorityLevel low = Enums.PriorityLevel.Low;
        Enums.PriorityLevel high = Enums.PriorityLevel.High;
        low.Equals(Enums.PriorityLevel.Low).Should().BeTrue();
        low.Equals(Enums.PriorityLevel.High).Should().BeFalse();
        high.Equals(Enums.PriorityLevel.High).Should().BeTrue();
    }

    [Test]
    public void Int_enum_implements_IEquatable_int()
    {
        Enums.OrderStatus.Pending.Equals(1).Should().BeTrue();
        Enums.OrderStatus.Pending.Equals(2).Should().BeFalse();
        Enums.OrderStatus.Completed.Equals(2).Should().BeTrue();

        Enums.PriorityLevel.Low.Equals(0).Should().BeTrue();
        Enums.PriorityLevel.Low.Equals(1).Should().BeFalse();
        Enums.PriorityLevel.High.Equals(1).Should().BeTrue();
    }

    [Test]
    public void Int_enum_equality_TSelf_TSelf()
    {
        Enums.OrderStatus pending = Enums.OrderStatus.Pending;
        Enums.OrderStatus completed = Enums.OrderStatus.Completed;
        (pending == Enums.OrderStatus.Pending).Should().BeTrue();
        (pending != completed).Should().BeTrue();
        (pending == completed).Should().BeFalse();

        Enums.PriorityLevel low = Enums.PriorityLevel.Low;
        Enums.PriorityLevel high = Enums.PriorityLevel.High;
        (low == Enums.PriorityLevel.Low).Should().BeTrue();
        (low != high).Should().BeTrue();
        (low == high).Should().BeFalse();
    }

    [Test]
    public void Int_enum_equality_TSelf_int_both_directions()
    {
        (Enums.OrderStatus.Pending == 1).Should().BeTrue();
        (Enums.OrderStatus.Pending != 1).Should().BeFalse();
        (Enums.OrderStatus.Pending == 2).Should().BeFalse();
        (1 == Enums.OrderStatus.Pending).Should().BeTrue();
        (2 != Enums.OrderStatus.Pending).Should().BeTrue();

        (Enums.PriorityLevel.Low == 0).Should().BeTrue();
        (Enums.PriorityLevel.High == 1).Should().BeTrue();
        (0 == Enums.PriorityLevel.Low).Should().BeTrue();
        (1 == Enums.PriorityLevel.High).Should().BeTrue();
    }

    [Test]
    public void Int_enum_ToString_uses_invariant_culture()
    {
        Enums.OrderStatus.Pending.ToString().Should().Be("1");
        Enums.OrderStatus.Completed.ToString().Should().Be("2");
        Enums.PriorityLevel.Low.ToString().Should().Be("0");
        Enums.PriorityLevel.High.ToString().Should().Be("1");
    }

    [Test]
    public void Int_enum_explicit_conversion_to_int()
    {
        ((int)Enums.OrderStatus.Pending).Should().Be(1);
        ((int)Enums.OrderStatus.Completed).Should().Be(2);
        ((int)Enums.PriorityLevel.Low).Should().Be(0);
        ((int)Enums.PriorityLevel.High).Should().Be(1);
    }

    [Test]
    public void Int_enum_override_Equals_object_and_GetHashCode()
    {
        Enums.OrderStatus pending = Enums.OrderStatus.FromValue(1);
        pending.Equals((object)Enums.OrderStatus.Pending).Should().BeTrue();
        pending.GetHashCode().Should().Be(Enums.OrderStatus.Pending.GetHashCode());

        Enums.PriorityLevel low = Enums.PriorityLevel.FromValue(0);
        low.Equals((object)Enums.PriorityLevel.Low).Should().BeTrue();
        low.GetHashCode().Should().Be(Enums.PriorityLevel.Low.GetHashCode());
    }

    // --- IncludeEnumValues: composed type with own + included instances ---

    [Test]
    public void IncludeEnumValues_List_contains_own_then_included_in_order()
    {
        Enums.BoxShadowKeyword.List.Count.Should().Be(5); // None, Inset (own) + Initial, Inherit, Unset (from GlobalKeyword)
        Enums.BoxShadowKeyword.Values.Length.Should().Be(5);

        Enums.BoxShadowKeyword.List[0].Should().BeSameAs(Enums.BoxShadowKeyword.None);
        Enums.BoxShadowKeyword.List[1].Should().BeSameAs(Enums.BoxShadowKeyword.Inset);
        Enums.BoxShadowKeyword.List[2].Should().BeSameAs(Enums.BoxShadowKeyword.Initial);
        Enums.BoxShadowKeyword.List[3].Should().BeSameAs(Enums.BoxShadowKeyword.Inherit);
        Enums.BoxShadowKeyword.List[4].Should().BeSameAs(Enums.BoxShadowKeyword.Unset);
    }

    [Test]
    public void IncludeEnumValues_TryFromValue_works_for_own_and_included()
    {
        Enums.BoxShadowKeyword.TryFromValue("none", out Enums.BoxShadowKeyword? none).Should().BeTrue();
        none.Should().BeSameAs(Enums.BoxShadowKeyword.None);

        Enums.BoxShadowKeyword.TryFromValue("inset", out Enums.BoxShadowKeyword? inset).Should().BeTrue();
        inset.Should().BeSameAs(Enums.BoxShadowKeyword.Inset);

        Enums.BoxShadowKeyword.TryFromValue("initial", out Enums.BoxShadowKeyword? initial).Should().BeTrue();
        initial.Should().BeSameAs(Enums.BoxShadowKeyword.Initial);

        Enums.BoxShadowKeyword.TryFromValue("inherit", out Enums.BoxShadowKeyword? inherit).Should().BeTrue();
        inherit.Should().BeSameAs(Enums.BoxShadowKeyword.Inherit);

        Enums.BoxShadowKeyword.TryFromValue("unset", out Enums.BoxShadowKeyword? unset).Should().BeTrue();
        unset.Should().BeSameAs(Enums.BoxShadowKeyword.Unset);

        Enums.BoxShadowKeyword.TryFromValue("unknown", out Enums.BoxShadowKeyword? _).Should().BeFalse();
    }

    [Test]
    public void IncludeEnumValues_TryFromName_works_for_own_and_included()
    {
        Enums.BoxShadowKeyword.TryFromName("None", out Enums.BoxShadowKeyword? none).Should().BeTrue();
        none.Should().BeSameAs(Enums.BoxShadowKeyword.None);

        Enums.BoxShadowKeyword.TryFromName("Initial", out Enums.BoxShadowKeyword? initial).Should().BeTrue();
        initial.Should().BeSameAs(Enums.BoxShadowKeyword.Initial);

        Enums.BoxShadowKeyword.TryFromName("Unknown", out Enums.BoxShadowKeyword? _).Should().BeFalse();
    }

    [Test]
    public void IncludeEnumValues_FromValue_and_FromName_work_for_included()
    {
        Enums.BoxShadowKeyword.FromValue("initial").Should().BeSameAs(Enums.BoxShadowKeyword.Initial);
        Enums.BoxShadowKeyword.FromName("Initial").Should().BeSameAs(Enums.BoxShadowKeyword.Initial);
    }

    [Test]
    public void IncludeEnumValues_Json_round_trips_own_and_included()
    {
        string noneJson = JsonSerializer.Serialize(Enums.BoxShadowKeyword.None);
        noneJson.Should().Be("\"none\"");
        JsonSerializer.Deserialize<Enums.BoxShadowKeyword>("\"none\"").Should().BeSameAs(Enums.BoxShadowKeyword.None);

        string initialJson = JsonSerializer.Serialize(Enums.BoxShadowKeyword.Initial);
        initialJson.Should().Be("\"initial\"");
        JsonSerializer.Deserialize<Enums.BoxShadowKeyword>("\"initial\"").Should().BeSameAs(Enums.BoxShadowKeyword.Initial);
    }

    [Test]
    public void IncludeEnumValues_Name_and_Value_match_for_included_instances()
    {
        Enums.BoxShadowKeyword.Initial.Name.Should().Be("Initial");
        Enums.BoxShadowKeyword.Initial.Value.Should().Be("initial");

        Enums.BoxShadowKeyword.None.Name.Should().Be("None");
        Enums.BoxShadowKeyword.None.Value.Should().Be("none");
    }

    [Test]
    public void IncludeEnumValues_included_instance_is_distinct_from_source_type()
    {
        Enums.BoxShadowKeyword.Initial.Should().NotBeSameAs(Enums.GlobalKeyword.Initial);
        Enums.BoxShadowKeyword.Initial.Value.Should().Be(Enums.GlobalKeyword.Initial.Value);
        ((string)Enums.BoxShadowKeyword.Initial).Should().Be((string)Enums.GlobalKeyword.Initial);
    }
}
