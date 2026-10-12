namespace I4Twins.Application.Aggregation;

public sealed record AggregatedBucket(DateTimeOffset StartTs, int Count, double Average, double Min, double Max);
