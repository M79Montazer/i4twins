namespace I4Twins.Domain.Readings;


public sealed class DuplicateFilter
{
    private readonly HashSet<ReadingKey> _seen = [];

    public bool IsDuplicate(SensorReading reading) => !_seen.Add(reading.Key);
}
