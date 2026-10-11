namespace I4Twins.Domain.Readings;

public static class EventTimeOrder
{
    public static IReadOnlyList<SensorReading> Sort(IEnumerable<SensorReading> readings) =>
    [
        .. readings
            .OrderBy(r => r.DeviceId, StringComparer.Ordinal)
            .ThenBy(r => r.Metric, StringComparer.Ordinal)
            .ThenBy(r => r.Timestamp)
            .ThenBy(r => r.Seq),
    ];
}
