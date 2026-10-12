using System.Globalization;
using I4Twins.Application.Aggregation;
using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Application.Aggregation;

public class AggregationServiceTests
{
    private sealed class StubSource(params SensorReading[] readings) : IAcceptableReadingSource
    {
        public IReadOnlyList<SensorReading> ReadAcceptable(string deviceId, string metric, DateTimeOffset from, DateTimeOffset to) =>
            readings;
    }

    private static SensorReading Reading(string ts, double value, long seq = 1, string deviceId = "PUMP-01", string metric = "temperature") =>
        new(deviceId, metric, DateTimeOffset.Parse(ts, styles: DateTimeStyles.AssumeUniversal), value, seq);

    private static AggregationResponse Aggregate(IAcceptableReadingSource source, string from, string to, int bucketSeconds = 300) =>
        new AggregationService(source).Aggregate(
            "PUMP-01", "temperature",
            DateTimeOffset.Parse(from, styles: DateTimeStyles.AssumeUniversal),
            DateTimeOffset.Parse(to, styles: DateTimeStyles.AssumeUniversal),
            bucketSeconds);

    [Fact]
    public void Buckets_AreAnchoredAtFrom_AndHalfOpen()
    {
        var response = Aggregate(
            new StubSource(
                Reading("2025-06-01T08:00:00Z", 20, seq: 1),
                Reading("2025-06-01T08:04:59Z", 25, seq: 2),
                Reading("2025-06-01T08:05:00Z", 30, seq: 3)),
            from: "2025-06-01T08:00:00Z",
            to: "2025-06-01T08:10:00Z");

        Assert.Equal(2, response.Buckets.Count);
        Assert.Equal("2025-06-01T08:00:00Z", response.Buckets[0].StartTs.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        Assert.Equal(2, response.Buckets[0].Count);
        Assert.Equal("2025-06-01T08:05:00Z", response.Buckets[1].StartTs.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        Assert.Equal(1, response.Buckets[1].Count);
    }

    [Fact]
    public void ReadingsAtTo_AreExcluded()
    {
        var response = Aggregate(
            new StubSource(Reading("2025-06-01T08:09:59Z", 20), Reading("2025-06-01T08:10:00Z", 30)),
            from: "2025-06-01T08:00:00Z",
            to: "2025-06-01T08:10:00Z",
            bucketSeconds: 600);

        var bucket = Assert.Single(response.Buckets);
        Assert.Equal(1, bucket.Count);
        Assert.Equal(20, bucket.Average);
    }

    [Fact]
    public void BucketStatistics_AreCorrect()
    {
        var response = Aggregate(
            new StubSource(
                Reading("2025-06-01T08:00:10Z", 10),
                Reading("2025-06-01T08:02:00Z", 20),
                Reading("2025-06-01T08:04:50Z", 30)),
            from: "2025-06-01T08:00:00Z",
            to: "2025-06-01T08:05:00Z");

        var bucket = Assert.Single(response.Buckets);
        Assert.Equal(3, bucket.Count);
        Assert.Equal(20, bucket.Average);
        Assert.Equal(10, bucket.Min);
        Assert.Equal(30, bucket.Max);
    }

    [Fact]
    public void EmptyBuckets_AreOmitted()
    {
        var response = Aggregate(
            new StubSource(
                Reading("2025-06-01T08:00:10Z", 10),
                Reading("2025-06-01T08:15:10Z", 30)),
            from: "2025-06-01T08:00:00Z",
            to: "2025-06-01T08:20:00Z",
            bucketSeconds: 300);

        Assert.Equal(2, response.Buckets.Count);
        Assert.Equal("2025-06-01T08:00:00Z", response.Buckets[0].StartTs.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        Assert.Equal("2025-06-01T08:15:00Z", response.Buckets[1].StartTs.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
    }

    [Fact]
    public void RangeWithNoReadings_ReturnsNoBuckets()
    {
        var response = Aggregate(new StubSource(), from: "2025-06-01T08:00:00Z", to: "2025-06-01T08:05:00Z");

        Assert.Empty(response.Buckets);
    }

    [Fact]
    public void Buckets_ListIsOrderedByStartTime()
    {
        var response = Aggregate(
            new StubSource(
                Reading("2025-06-01T08:16:00Z", 30),
                Reading("2025-06-01T08:01:00Z", 10)),
            from: "2025-06-01T08:00:00Z",
            to: "2025-06-01T08:20:00Z",
            bucketSeconds: 300);

        Assert.True(response.Buckets[0].StartTs < response.Buckets[1].StartTs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveBucketSeconds_AreRejected(int bucketSeconds)
    {
        var service = new AggregationService(new StubSource());

        Assert.Throws<ArgumentException>(() => service.Aggregate(
            "PUMP-01", "temperature",
            DateTimeOffset.Parse("2025-06-01T08:00:00Z", styles: DateTimeStyles.AssumeUniversal),
            DateTimeOffset.Parse("2025-06-01T08:05:00Z", styles: DateTimeStyles.AssumeUniversal),
            bucketSeconds));
    }

    [Fact]
    public void EmptyOrEqualRange_IsRejected()
    {
        var service = new AggregationService(new StubSource());
        var from = DateTimeOffset.Parse("2025-06-01T08:05:00Z", styles: DateTimeStyles.AssumeUniversal);
        var to = DateTimeOffset.Parse("2025-06-01T08:00:00Z", styles: DateTimeStyles.AssumeUniversal);

        Assert.Throws<ArgumentException>(() => service.Aggregate("PUMP-01", "temperature", from, to, 300));
        Assert.Throws<ArgumentException>(() => service.Aggregate("PUMP-01", "temperature", from, from, 300));
    }

    [Fact]
    public void BlankDeviceIdOrMetric_AreRejected()
    {
        var service = new AggregationService(new StubSource());
        var from = DateTimeOffset.Parse("2025-06-01T08:00:00Z", styles: DateTimeStyles.AssumeUniversal);
        var to = DateTimeOffset.Parse("2025-06-01T08:05:00Z", styles: DateTimeStyles.AssumeUniversal);

        Assert.Throws<ArgumentException>(() => service.Aggregate("", "temperature", from, to, 300));
        Assert.Throws<ArgumentException>(() => service.Aggregate("PUMP-01", " ", from, to, 300));
    }
}
