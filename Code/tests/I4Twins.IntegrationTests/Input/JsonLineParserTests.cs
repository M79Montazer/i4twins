using System.Text.Json;
using I4Twins.Domain.Readings;
using I4Twins.Infrastructure.Input;

namespace I4Twins.IntegrationTests.Input;

public class JsonLineParserTests
{
    private readonly JsonLineParser _parser = new();

    private static RawReading ParseOrThrow(string line)
    {
        var result = new JsonLineParser().Parse(line);
        var parsed = Assert.IsType<LineParsed>(result);
        return parsed.Reading;
    }

    [Fact]
    public void ValidLine_MapsAllFields()
    {
        var raw = ParseOrThrow(
            """{"deviceId": "PUMP-01", "metric": "temperature", "ts": "2025-06-01T08:33:00Z", "value": 67.21, "seq": 1199}""");

        Assert.Equal(RawFieldType.Text, raw.DeviceId.Type);
        Assert.Equal("PUMP-01", raw.DeviceId.Text);
        Assert.Equal("temperature", raw.Metric.Text);
        Assert.Equal("2025-06-01T08:33:00Z", raw.Timestamp.Text);
        Assert.Equal(RawFieldType.Number, raw.Value.Type);
        Assert.Equal(67.21, raw.Value.Number);
        Assert.Equal(RawFieldType.Integer, raw.Seq.Type);
        Assert.Equal(1199, raw.Seq.Integer);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"deviceId\": }")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void MalformedOrNonObjectLines_AreMalformed(string line)
    {
        var result = _parser.Parse(line);

        var malformed = Assert.IsType<LineMalformed>(result);
        Assert.False(string.IsNullOrWhiteSpace(malformed.Reason));
    }

    [Fact]
    public void MissingProperty_IsMissingKind()
    {
        var raw = ParseOrThrow("""{"metric": "temperature", "ts": "2025-06-01T08:03:20Z", "value": 69.0, "seq": 1076}""");

        Assert.Equal(RawFieldType.Missing, raw.DeviceId.Type);
    }

    [Fact]
    public void NullProperties_AreNullKind()
    {
        var raw = ParseOrThrow(
            """{"deviceId": "PUMP-02", "metric": "pressure", "ts": "2025-06-01T08:02:00Z", "value": null, "seq": null}""");

        Assert.Equal(RawFieldType.Null, raw.Value.Type);
        Assert.Equal(RawFieldType.Null, raw.Seq.Type);
    }

    [Fact]
    public void StringValue_IsTextKind()
    {
        var raw = ParseOrThrow(
            """{"deviceId": "COMP-01", "metric": "vibration", "ts": "2025-06-01T08:08:20Z", "value": "NaN", "seq": 1200}""");

        Assert.Equal(RawFieldType.Text, raw.Value.Type);
        Assert.Equal("NaN", raw.Value.Text);
    }

    [Fact]
    public void FractionalSeq_IsNumberKind_NotInteger()
    {
        var raw = ParseOrThrow("""{"deviceId": "X", "metric": "m", "ts": "2025-06-01T08:00:00Z", "value": 1.0, "seq": 1199.5}""");

        Assert.Equal(RawFieldType.Number, raw.Seq.Type);
    }

    [Fact]
    public void LargeFractionalValue_IsNumberKind_WithFullMagnitude()
    {
        var raw = ParseOrThrow("""{"deviceId": "X", "metric": "temperature", "ts": "2025-06-01T08:00:00Z", "value": 1000000.0, "seq": 1}""");

        Assert.Equal(RawFieldType.Number, raw.Value.Type);
        Assert.Equal(1000000.0, raw.Value.Number);
    }

    [Fact]
    public void NegativeIntegerValue_IsIntegerKind()
    {
        var raw = ParseOrThrow("""{"deviceId": "X", "metric": "vibration", "ts": "2025-06-01T08:00:00Z", "value": -9999, "seq": 1}""");

        Assert.Equal(RawFieldType.Integer, raw.Value.Type);
        Assert.Equal(-9999, raw.Value.Integer);
    }

    [Fact]
    public void BooleanValue_IsOtherKind()
    {
        var raw = ParseOrThrow("""{"deviceId": "X", "metric": "m", "ts": "2025-06-01T08:00:00Z", "value": true, "seq": 1}""");

        Assert.Equal(RawFieldType.Other, raw.Value.Type);
    }
}
