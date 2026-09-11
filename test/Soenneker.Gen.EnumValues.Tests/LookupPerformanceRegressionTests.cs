using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;

namespace Soenneker.Gen.EnumValues.Tests;

public sealed class LookupPerformanceRegressionTests
{
    [Test]
    public void Span_dispatch_checks_entire_value_and_name()
    {
        foreach (var value in LookupProbe.List)
        {
            LookupProbe.TryFromValue(value.Value.AsSpan(), out var parsed).Should().BeTrue();
            parsed.Should().BeSameAs(value);
            LookupProbe.TryFromName(value.Name.AsSpan(), out parsed).Should().BeTrue();
            parsed.Should().BeSameAs(value);
        }
        LookupProbe.TryFromValue("other--value-03".AsSpan(), out _).Should().BeFalse();
        LookupProbe.TryFromValue("prefix-value-09".AsSpan(), out _).Should().BeFalse();
        LookupProbe.TryFromValue((string?)null, out _).Should().BeFalse();
    }

    [Test]
    public void Json_dispatch_handles_every_segment_boundary_and_escaped_property_names()
    {
        var options = new JsonSerializerOptions();
        var converter = (JsonConverter<LookupProbe>)options.GetConverter(typeof(LookupProbe));
        foreach (var value in LookupProbe.List)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(value.Value);
            for (int split = 1; split < json.Length; split++)
            {
                var reader = new Utf8JsonReader(Split(json, split));
                reader.Read();
                converter.Read(ref reader, typeof(LookupProbe), options).Should().BeSameAs(value);
            }
            byte[] propertyJson = Encoding.UTF8.GetBytes("{" + Encoding.UTF8.GetString(json) + ":0}");
            var propertyReader = new Utf8JsonReader(Split(propertyJson, propertyJson.Length / 2));
            propertyReader.Read();
            propertyReader.Read();
            converter.ReadAsPropertyName(ref propertyReader, typeof(LookupProbe), options).Should().BeSameAs(value);
        }
        Action unknown = () => JsonSerializer.Deserialize<LookupProbe>("\"prefix-value-09\"");
        unknown.Should().Throw<JsonException>();
    }

    [Test]
    public void Short_escaped_and_segmented_reads_do_not_allocate_after_warmup()
    {
        var options = new JsonSerializerOptions();
        var converter = (JsonConverter<LookupProbe>)options.GetConverter(typeof(LookupProbe));
        byte[] json = "\"prefix-value-\\u00303\""u8.ToArray();
        var sequence = Split(json, 18);
        ReadRepeatedly(converter, options, json, sequence);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = ReadRepeatedly(converter, options, json, sequence);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        result.Should().BeSameAs(LookupProbe.Three);
        allocated.Should().Be(0);
    }

    private static LookupProbe? ReadRepeatedly(JsonConverter<LookupProbe> converter, JsonSerializerOptions options, byte[] json, ReadOnlySequence<byte> sequence)
    {
        LookupProbe? result = null;
        for (int i = 0; i < 1000; i++)
        {
            var reader = new Utf8JsonReader(json);
            reader.Read();
            result = converter.Read(ref reader, typeof(LookupProbe), options);
            reader = new Utf8JsonReader(sequence);
            reader.Read();
            result = converter.Read(ref reader, typeof(LookupProbe), options);
        }
        return result;
    }

    private static ReadOnlySequence<byte> Split(byte[] bytes, int split)
    {
        var first = new Segment(bytes.AsMemory(0, split));
        var last = first.Append(bytes.AsMemory(split));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;
        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}

[EnumValue<string>]
public sealed partial class LookupProbe
{
    public static readonly LookupProbe Empty = new("");
    public static readonly LookupProbe Zero = new("prefix-value-00");
    public static readonly LookupProbe One = new("prefix-value-01");
    public static readonly LookupProbe Two = new("prefix-value-02");
    public static readonly LookupProbe Three = new("prefix-value-03");
    public static readonly LookupProbe Unicode = new("éclair");
    public static readonly LookupProbe Emoji = new("😀");
    public static readonly LookupProbe Quote = new("quoted\"value");
    public static readonly LookupProbe Long = new("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789");
}
