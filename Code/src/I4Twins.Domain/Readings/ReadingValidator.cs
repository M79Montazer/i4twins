using System.Globalization;

namespace I4Twins.Domain.Readings;

public sealed record ReadingValidationResult(SensorReading? Reading, string? Error)
{
    public bool IsValid => Reading is not null;

    public static ReadingValidationResult Ok(SensorReading reading) => new(reading, null);
    public static ReadingValidationResult Invalid(string error) => new(null, error);
}


public static class ReadingValidator
{
    public static ReadingValidationResult Validate(RawReading raw)
    {
        if (!IsIdentifier("deviceId", raw.DeviceId, out var deviceId, out var deviceError))
        {
            return ReadingValidationResult.Invalid(deviceError);
        }

        if (!IsIdentifier("metric", raw.Metric, out var metric, out var metricError))
        {
            return ReadingValidationResult.Invalid(metricError);
        }

        if (!TryValidateTimestamp(raw.Timestamp, out var timestamp, out var timestampError))
        {
            return ReadingValidationResult.Invalid(timestampError);
        }

        if (raw.Value.Type is not (RawFieldType.Number or RawFieldType.Integer))
        {
            return ReadingValidationResult.Invalid("value must be a number");
        }

        if (raw.Seq.Type is not RawFieldType.Integer)
        {
            return ReadingValidationResult.Invalid("seq must be an integer");
        }

        var value = raw.Value.Type == RawFieldType.Integer ? raw.Value.Integer : raw.Value.Number;
        var reading = new SensorReading(deviceId, metric, timestamp, value, raw.Seq.Integer);
        return ReadingValidationResult.Ok(reading);
    }

    private static bool IsIdentifier(string fieldName, RawField field, out string value, out string error)
    {
        if (field.Type is not RawFieldType.Text)
        {
            value = string.Empty;
            error = $"{fieldName} must be a non-empty string";
            return false;
        }

        value = field.Text!;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{fieldName} must be a non-empty string";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateTimestamp(RawField field, out DateTimeOffset timestamp, out string error)
    {
        timestamp = default;
        if (field.Type is not RawFieldType.Text)
        {
            error = "ts must be a timestamp string";
            return false;
        }

        if (!DateTime.TryParse(field.Text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
            || parsed.Kind != DateTimeKind.Utc)
        {
            error = "ts must be a timestamp with an explicit UTC offset";
            return false;
        }

        timestamp = new DateTimeOffset(parsed, TimeSpan.Zero);
        error = string.Empty;
        return true;
    }
}
