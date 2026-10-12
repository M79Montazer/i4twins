namespace I4Twins.Application.Aggregation;

public sealed record AggregationResponse(string DeviceId, string Metric, int BucketSeconds, IReadOnlyList<AggregatedBucket> Buckets);
