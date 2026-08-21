using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class SerializationDispatchBenchmark
{
    private byte[] _json = null!;
    private JsonSerializerOptions _generatedOptions = null!;
    private JsonSerializerOptions _linearOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _json = JsonSerializer.SerializeToUtf8Bytes(Enums.LargeStringCode.Unicode);
        _generatedOptions = new JsonSerializerOptions();
        _linearOptions = new JsonSerializerOptions
        {
            Converters = { new LinearLargeStringCodeConverter() }
        };
    }

    [Benchmark(Baseline = true)]
    public Enums.LargeStringCode GeneratedDeserialize()
        => JsonSerializer.Deserialize<Enums.LargeStringCode>(_json, _generatedOptions)!;

    [Benchmark]
    public Enums.LargeStringCode PreviousLinearDeserialize()
        => JsonSerializer.Deserialize<Enums.LargeStringCode>(_json, _linearOptions)!;

    [Benchmark]
    public byte[] GeneratedSerialize()
        => JsonSerializer.SerializeToUtf8Bytes(Enums.LargeStringCode.Unicode, _generatedOptions);

    [Benchmark]
    public byte[] PreviousLinearSerialize()
        => JsonSerializer.SerializeToUtf8Bytes(Enums.LargeStringCode.Unicode, _linearOptions);

    private sealed class LinearLargeStringCodeConverter : JsonConverter<Enums.LargeStringCode>
    {
        public override Enums.LargeStringCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.ValueTextEquals("alpha"u8)) return Enums.LargeStringCode.Alpha;
            if (reader.ValueTextEquals("bravo"u8)) return Enums.LargeStringCode.Bravo;
            if (reader.ValueTextEquals("charlie"u8)) return Enums.LargeStringCode.Charlie;
            if (reader.ValueTextEquals("delta"u8)) return Enums.LargeStringCode.Delta;
            if (reader.ValueTextEquals("echo"u8)) return Enums.LargeStringCode.Echo;
            if (reader.ValueTextEquals("foxtrot"u8)) return Enums.LargeStringCode.Foxtrot;
            if (reader.ValueTextEquals("golf"u8)) return Enums.LargeStringCode.Golf;
            if (reader.ValueTextEquals("hotel"u8)) return Enums.LargeStringCode.Hotel;
            if (reader.ValueTextEquals("quoted\"value"u8)) return Enums.LargeStringCode.Quoted;
            if (reader.ValueTextEquals("éclair"u8)) return Enums.LargeStringCode.Unicode;
            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Enums.LargeStringCode value, JsonSerializerOptions options)
        {
            if (ReferenceEquals(value, Enums.LargeStringCode.Alpha)) { writer.WriteStringValue("alpha"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Bravo)) { writer.WriteStringValue("bravo"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Charlie)) { writer.WriteStringValue("charlie"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Delta)) { writer.WriteStringValue("delta"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Echo)) { writer.WriteStringValue("echo"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Foxtrot)) { writer.WriteStringValue("foxtrot"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Golf)) { writer.WriteStringValue("golf"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Hotel)) { writer.WriteStringValue("hotel"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Quoted)) { writer.WriteStringValue("quoted\"value"u8); return; }
            if (ReferenceEquals(value, Enums.LargeStringCode.Unicode)) { writer.WriteStringValue("éclair"u8); return; }
            writer.WriteStringValue(value.Value);
        }
    }
}
