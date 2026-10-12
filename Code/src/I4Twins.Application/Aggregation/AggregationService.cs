using I4Twins.Domain.Readings;

namespace I4Twins.Application.Aggregation;

public sealed class AggregationService(IAcceptableReadingSource source)
{
    public AggregationResponse Aggregate(string deviceId, string metric, DateTimeOffset from, DateTimeOffset to, int bucketSeconds)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("deviceId must be a non-empty string", nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(metric))
        {
            throw new ArgumentException("metric must be a non-empty string", nameof(metric));
        }

        if (bucketSeconds <= 0)
        {
            throw new ArgumentException("bucketSeconds must be greater than 0", nameof(bucketSeconds));
        }

        if (from >= to)
        {
            throw new ArgumentException("from must be earlier than to", nameof(to));
        }

        var inRange = source.ReadAcceptable(deviceId, metric, from, to)
            .Where(r => r.Timestamp >= from && r.Timestamp < to);

        var bucketTicks = (long)bucketSeconds * TimeSpan.TicksPerSecond;
        var buckets = new Dictionary<long, List<SensorReading>>();
        foreach (var reading in inRange)
        {
            var index = (reading.Timestamp.Ticks - from.Ticks) / bucketTicks;
            if (!buckets.TryGetValue(index, out var members))
            {
                members = [];
                buckets[index] = members;
            }

            members.Add(reading);
        }

        var aggregated = buckets
            .OrderBy(pair => pair.Key)
            .Select(pair =>
            {
                var values = pair.Value.Select(r => r.Value).ToList();
                return new AggregatedBucket(
                    StartTs: new DateTimeOffset(from.Ticks + pair.Key * bucketTicks, from.Offset),
                    Count: values.Count,
                    Average: values.Average(),
                    Min: values.Min(),
                    Max: values.Max());
            })
            .ToList();

        return new AggregationResponse(deviceId, metric, bucketSeconds, aggregated);
    }
}
