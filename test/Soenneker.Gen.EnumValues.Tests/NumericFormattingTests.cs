using System;
using System.Globalization;
using AwesomeAssertions;

namespace Soenneker.Gen.EnumValues.Tests;

public sealed class NumericFormattingTests
{
    [Test]
    public void Known_integer_values_use_invariant_text()
    {
        SignedNumbers.Minimum.ToString().Should().Be(int.MinValue.ToString(CultureInfo.InvariantCulture));
        SignedNumbers.Large.ToString().Should().Be("1000");
        SignedNumbers.Zero.ToString().Should().Be("0");
        SignedNumbers.Small.ToString().Should().Be("299");
        LongNumbers.Minimum.ToString().Should().Be(long.MinValue.ToString(CultureInfo.InvariantCulture));
        LongNumbers.Maximum.ToString().Should().Be(long.MaxValue.ToString(CultureInfo.InvariantCulture));
        UnsignedNumbers.Maximum.ToString().Should().Be(uint.MaxValue.ToString(CultureInfo.InvariantCulture));
        ULongNumbers.Maximum.ToString().Should().Be(ulong.MaxValue.ToString(CultureInfo.InvariantCulture));
    }

    [Test]
    public void Unknown_and_default_values_keep_runtime_formatting()
    {
        default(SignedNumbers).ToString().Should().Be("0");
        SignedNumbers.Create(-999).ToString().Should().Be("-999");
        LongNumbers.Create(123456789012).ToString().Should().Be("123456789012");
        UnsignedNumbers.Create(999).ToString().Should().Be("999");
        ULongNumbers.Create(123456789012).ToString().Should().Be("123456789012");
    }

    [Test]
    public void A_custom_value_getter_is_evaluated_once_even_on_a_miss()
    {
        var number = CountingNumber.Create(123456);
        number.ToString().Should().Be("123456");
        number.Reads.Should().Be(1);
        var known = CountingNumber.Create(1000);
        known.ToString().Should().Be("1000");
        known.Reads.Should().Be(1);
    }

    [Test]
    public void Known_values_do_not_allocate_strings_after_warmup()
    {
        int total = 0;
        for (int i = 0; i < 1000; i++) total += SignedNumbers.Large.ToString().Length;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) total += SignedNumbers.Large.ToString().Length;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        total.Should().Be(8000);
        allocated.Should().Be(0);
    }
}

[EnumValue]
public partial struct SignedNumbers
{
    public static readonly SignedNumbers Minimum = new(int.MinValue);
    public static readonly SignedNumbers Large = new(1000);
    public static readonly SignedNumbers Zero = new(0);
    public static readonly SignedNumbers Small = new(299);
    public static SignedNumbers Create(int value) => new(value);
}

[EnumValue<long>]
public sealed partial class LongNumbers
{
    public static readonly LongNumbers Minimum = new(long.MinValue);
    public static readonly LongNumbers Maximum = new(long.MaxValue);
    public static LongNumbers Create(long value) => new(value);
}

[EnumValue<uint>]
public partial struct UnsignedNumbers
{
    public static readonly UnsignedNumbers Maximum = new(uint.MaxValue);
    public static UnsignedNumbers Create(uint value) => new(value);
}

[EnumValue<ulong>]
public sealed partial class ULongNumbers
{
    public static readonly ULongNumbers Maximum = new(ulong.MaxValue);
    public static ULongNumbers Create(ulong value) => new(value);
}

[EnumValue]
public sealed partial class CountingNumber
{
    private int _value;
    public int Reads { get; private set; }
    public int Value { get { Reads++; return _value; } private set => _value = value; }
    public static readonly CountingNumber Known = new(1000);
    public static CountingNumber Create(int value) => new(value);
}
