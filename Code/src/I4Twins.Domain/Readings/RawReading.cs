namespace I4Twins.Domain.Readings;


public sealed record RawReading(
    RawField DeviceId,
    RawField Metric,
    RawField Timestamp,
    RawField Value,
    RawField Seq
    );
