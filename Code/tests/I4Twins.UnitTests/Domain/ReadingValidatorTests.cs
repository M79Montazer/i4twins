using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Domain;

public class ReadingValidatorTests
{
    private static RawField Text(string value) => RawField.Of(value);
    private static RawField Missing() => new(RawFieldType.Missing);
    private static RawField Null() => new(RawFieldType.Null);
    private static RawField Number(double value) => RawField.Of(value);
    private static RawField Integer(long value) => RawField.Of(value);

    private static RawReading Raw(
        RawField? deviceId = null,
        RawField? metric = null,
        RawField? ts = null,
        RawField? value = null,
        RawField? seq = null) => new(
        deviceId ?? Text("PUMP-01"),
        metric ?? Text("temperature"),
        ts ?? Text("2025-06-01T08:00:00Z"),
        value ?? Number(67.21),
        seq ?? Integer(1199));

    private const string ValidTimestamp = "2025-06-01T08:00:00Z";

    [Fact]
    public void ValidRecord_ProducesReadingWithIdentity()
    {
        var result = ReadingValidator.Validate(Raw());

        Assert.True(result.IsValid);
        var reading = result.Reading!;
        Assert.Equal("PUMP-01", reading.DeviceId);
        Assert.Equal("temperature", reading.Metric);
        Assert.Equal(new DateTimeOffset(2025, 6, 1, 8, 0, 0, TimeSpan.Zero), reading.Timestamp);
        Assert.Equal(67.21, reading.Value);
        Assert.Equal(1199, reading.Seq);
        Assert.Equal(new ReadingKey("PUMP-01", "temperature", reading.Timestamp, 1199), reading.Key);
    }

    [Fact]
    public void ExplicitNonUtcOffset_IsConvertedToUtc()
    {
        var result = ReadingValidator.Validate(Raw(ts: Text("2025-06-01T10:00:00+02:00")));

        Assert.True(result.IsValid);
        Assert.Equal(new DateTimeOffset(2025, 6, 1, 8, 0, 0, TimeSpan.Zero), result.Reading!.Timestamp);
    }

    [Theory]
    [InlineData(null, "missing")]
    [InlineData("", "empty")]
    [InlineData("   ", "whitespace")]
    public void DeviceId_MissingOrEmpty_IsInvalid(string? deviceId, string _)
    {
        var field = deviceId is null ? Missing() : Text(deviceId);

        var result = ReadingValidator.Validate(Raw(deviceId: field));

        Assert.False(result.IsValid);
        Assert.Contains("deviceId", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingMetric_IsInvalid()
    {
        var result = ReadingValidator.Validate(Raw(metric: Missing()));

        Assert.False(result.IsValid);
        Assert.Contains("metric", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyMetric_IsInvalid()
    {
        var result = ReadingValidator.Validate(Raw(metric: Text("")));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("2025-06-31T08:04:10Z")]
    [InlineData("2025-06-01T08:00:05")] 
    [InlineData("not-a-date")]
    [InlineData("2025-13-01T00:00:00Z")]
    public void InvalidTimestampText_IsRejected(string timestamp)
    {
        var result = ReadingValidator.Validate(Raw(ts: Text(timestamp)));

        Assert.False(result.IsValid);
        Assert.Contains("ts", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonTextTimestamp_IsRejected()
    {
        var result = ReadingValidator.Validate(Raw(ts: Number(20250601)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NullTimestamp_IsRejected()
    {
        var result = ReadingValidator.Validate(Raw(ts: Null()));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void IntegerValue_IsANumber()
    {
        var result = ReadingValidator.Validate(Raw(value: Integer(5)));

        Assert.True(result.IsValid);
        Assert.Equal(5, result.Reading!.Value);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("67.21")]
    public void TextualValue_IsRejected(string text)
    {
        var result = ReadingValidator.Validate(Raw(value: Text(text)));

        Assert.False(result.IsValid);
        Assert.Contains("value", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NullValue_IsRejected()
    {
        var result = ReadingValidator.Validate(Raw(value: Null()));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MissingValue_IsRejected()
    {
        var result = ReadingValidator.Validate(Raw(value: Missing()));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void FractionalSeq_IsRejected()
    {
        var result = ReadingValidator.Validate(Raw(seq: Number(1199.5)));

        Assert.False(result.IsValid);
        Assert.Contains("seq", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NullSeq_IsRejected()
    {
        var result = ReadingValidator.Validate(Raw(seq: Null()));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TextualSeq_IsRejected()
    {
        var result = ReadingValidator.Validate(Raw(seq: Text("1199")));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NegativeFiniteValue_IsValid_DataDecidesPlausibility()
    {
        var result = ReadingValidator.Validate(Raw(value: Number(-9999)));

        Assert.True(result.IsValid);
    }
}
