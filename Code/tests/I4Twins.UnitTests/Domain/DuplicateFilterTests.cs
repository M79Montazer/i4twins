using System.Globalization;
using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Domain;

public class DuplicateFilterTests
{
    private static SensorReading Reading(
        string deviceId = "PUMP-01",
        string metric = "temperature",
        string ts = "2025-06-01T08:00:00Z",
        double value = 67.21,
        long seq = 1199) => new(
        deviceId,
        metric,
        DateTimeOffset.Parse(ts, styles: DateTimeStyles.AssumeUniversal),
        value,
        seq);

    [Fact]
    public void FirstOccurrence_IsAccepted()
    {
        var filter = new DuplicateFilter();

        Assert.False(filter.IsDuplicate(Reading()));
    }

    [Fact]
    public void IdenticalRepeat_IsDuplicate()
    {
        var filter = new DuplicateFilter();
        filter.IsDuplicate(Reading());

        Assert.True(filter.IsDuplicate(Reading()));
    }

    [Fact]
    public void SameIdentityWithDifferentValue_IsDuplicate_FirstWins()
    {
        var filter = new DuplicateFilter();
        filter.IsDuplicate(Reading(value: 81.2));

        Assert.True(filter.IsDuplicate(Reading(value: 82.4)));
    }

    [Fact]
    public void SameTimestampButDifferentSeq_IsNotADuplicate()
    {
        var filter = new DuplicateFilter();
        filter.IsDuplicate(Reading(seq: 1199));

        Assert.False(filter.IsDuplicate(Reading(seq: 1200)));
    }

    [Fact]
    public void DifferentDevice_IsNotADuplicate()
    {
        var filter = new DuplicateFilter();
        filter.IsDuplicate(Reading(deviceId: "PUMP-01"));

        Assert.False(filter.IsDuplicate(Reading(deviceId: "PUMP-02")));
    }

    [Fact]
    public void DifferentMetric_IsNotADuplicate()
    {
        var filter = new DuplicateFilter();
        filter.IsDuplicate(Reading(metric: "temperature"));

        Assert.False(filter.IsDuplicate(Reading(metric: "pressure")));
    }

    [Fact]
    public void DifferentTimestamp_IsNotADuplicate()
    {
        var filter = new DuplicateFilter();
        filter.IsDuplicate(Reading(ts: "2025-06-01T08:00:00Z"));

        Assert.False(filter.IsDuplicate(Reading(ts: "2025-06-01T08:00:05Z")));
    }
}
