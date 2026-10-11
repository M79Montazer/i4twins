namespace I4Twins.Domain.Readings;


public readonly record struct ReadingKey(string DeviceId, string Metric, DateTimeOffset Timestamp, long Seq);
