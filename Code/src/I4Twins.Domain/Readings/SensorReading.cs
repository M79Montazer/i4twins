namespace I4Twins.Domain.Readings;

public sealed record SensorReading(string DeviceId, string Metric, DateTimeOffset Timestamp, double Value, long Seq)
{
    public ReadingKey Key => new(DeviceId, Metric, Timestamp, Seq);
}
