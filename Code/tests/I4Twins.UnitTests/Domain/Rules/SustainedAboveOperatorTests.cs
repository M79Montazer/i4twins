using System.Globalization;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Rules.Operators;
using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Domain.Rules;

public class SustainedAboveOperatorTests
{
    private readonly SustainedAboveOperator ruleOperator = new();

    private static SensorReading Reading(double value, string ts, long seq, string deviceId = "PUMP-01", string metric = "temperature") =>
        new(deviceId, metric, DateTimeOffset.Parse(ts, styles: DateTimeStyles.AssumeUniversal), value, seq);

    private static RuleDefinition Rule(double threshold = 80, double durationSeconds = 30, string? deviceId = null) =>
        new("R2", "sustained", true, "temperature", deviceId, "SustainedAbove", threshold, null, null, durationSeconds);

    private static StatefulStreamResult Evaluate(params SensorReading[] readings) =>
        new SustainedAboveOperator().EvaluateStream("PUMP-01", "temperature", readings, Rule());

    [Fact]
    public void Episode_StartsAtFirstAboveThreshold_AndEndsAtFirstAtOrBelow()
    {
        var result = Evaluate(
            Reading(79, "2025-06-01T08:00:00Z", 1),
            Reading(85, "2025-06-01T08:00:10Z", 2),
            Reading(90, "2025-06-01T08:00:20Z", 3),
            Reading(80, "2025-06-01T08:01:00Z", 4));

        var episode = Assert.Single(result.Episodes);
        Assert.Equal(DateTimeOffset.Parse("2025-06-01T08:00:10Z", styles: DateTimeStyles.AssumeUniversal), episode.StartTs);
        Assert.Equal(DateTimeOffset.Parse("2025-06-01T08:01:00Z", styles: DateTimeStyles.AssumeUniversal), episode.EndTs);
        Assert.Equal(90, episode.PeakValue);
    }

    [Fact]
    public void Episode_EndOfInput_WhileActive_EndsAtLastAboveReading()
    {
        var result = Evaluate(
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(90, "2025-06-01T08:00:40Z", 2));

        var episode = Assert.Single(result.Episodes);
        Assert.Equal(DateTimeOffset.Parse("2025-06-01T08:00:40Z", styles: DateTimeStyles.AssumeUniversal), episode.EndTs);
        Assert.Equal(40, (episode.EndTs - episode.StartTs).TotalSeconds);
        Assert.True(episode.Qualifies);
    }

    [Theory]
    [InlineData(20, false)]
    [InlineData(30, true)]
    [InlineData(40, true)]
    public void Episode_QualifiesWhenDurationIsAtLeastConfigured(double seconds, bool expectedQualifies)
    {
        var result = Evaluate(
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(70, $"2025-06-01T08:00:{seconds:00}Z", 2));

        var episode = Assert.Single(result.Episodes);
        Assert.Equal(expectedQualifies, episode.Qualifies);
        Assert.Equal(expectedQualifies, result.Violations.Count > 0);
    }

    [Fact]
    public void ExactlyAtThreshold_IsNotAbove_AndTerminatesTheEpisode()
    {
        var result = Evaluate(
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(80, "2025-06-01T08:00:10Z", 2));

        var episode = Assert.Single(result.Episodes);
        Assert.Equal(DateTimeOffset.Parse("2025-06-01T08:00:10Z", styles: DateTimeStyles.AssumeUniversal), episode.EndTs);
        Assert.Equal(10, (episode.EndTs - episode.StartTs).TotalSeconds);
        Assert.False(episode.Qualifies);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void MultipleEpisodes_AreDetectedIndependently()
    {
        var result = Evaluate(
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(75, "2025-06-01T08:00:10Z", 2),
            Reading(90, "2025-06-01T08:00:20Z", 3),
            Reading(70, "2025-06-01T08:00:30Z", 4));

        Assert.Equal(2, result.Episodes.Count);
        Assert.All(result.Episodes, e => Assert.False(e.Qualifies));
    }

    [Fact]
    public void QualifyingEpisode_MarksEveryAboveThresholdReadingRetroactively()
    {
        var result = Evaluate(
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(90, "2025-06-01T08:00:20Z", 2),
            Reading(80, "2025-06-01T08:00:40Z", 3));

        Assert.True(result.Episodes[0].Qualifies);
        Assert.Equal(2, result.Violations.Count);
        Assert.All(result.Violations.Values, v => Assert.Equal("R2", v.RuleId));
        Assert.All(result.Violations.Values, v => Assert.False(string.IsNullOrWhiteSpace(v.Reason)));
        Assert.DoesNotContain(Reading(80, "2025-06-01T08:00:40Z", 3).Key, result.Violations.Keys);
    }

    [Fact]
    public void NonQualifyingEpisode_MarksNothing()
    {
        var result = Evaluate(
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(90, "2025-06-01T08:00:10Z", 2),
            Reading(70, "2025-06-01T08:00:20Z", 3));

        Assert.Single(result.Episodes);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void SamplingGap_DoesNotInterruptTheEpisode_ContinuityAssumption()
    {
        var result = Evaluate(
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(86, "2025-06-01T08:20:00Z", 2),
            Reading(70, "2025-06-01T08:21:00Z", 3));

        var episode = Assert.Single(result.Episodes);
        Assert.True(episode.Qualifies);
        Assert.Equal(2, result.Violations.Count);
    }

    [Fact]
    public void OutOfOrderInput_WhenSortedFirst_ProducesTheSameResult()
    {
        var ordered = new[]
        {
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(90, "2025-06-01T08:00:20Z", 2),
            Reading(80, "2025-06-01T08:00:40Z", 3),
        };

        var fromSorted = ruleOperator.EvaluateStream("PUMP-01", "temperature", ordered, Rule());
        var fromResorted = ruleOperator.EvaluateStream(
            "PUMP-01", "temperature", EventTimeOrder.Sort([ordered[2], ordered[0], ordered[1]]), Rule());

        Assert.Equal(fromSorted.Episodes, fromResorted.Episodes);
        Assert.Equal(
            fromSorted.Violations.OrderBy(kv => kv.Key.Timestamp),
            fromResorted.Violations.OrderBy(kv => kv.Key.Timestamp));
    }

    [Fact]
    public void Devices_DoNotShareState()
    {
        var readings = new[]
        {
            Reading(85, "2025-06-01T08:00:00Z", 1, deviceId: "PUMP-01"),
            Reading(70, "2025-06-01T08:00:10Z", 2, deviceId: "PUMP-02"),
            Reading(75, "2025-06-01T08:00:50Z", 3, deviceId: "PUMP-01"),
        };

        var pump01 = ruleOperator.EvaluateStream("PUMP-01", "temperature",
            readings.Where(r => r.DeviceId == "PUMP-01").ToArray(), Rule());
        var pump02 = ruleOperator.EvaluateStream("PUMP-02", "temperature",
            readings.Where(r => r.DeviceId == "PUMP-02").ToArray(), Rule());

        var episode = Assert.Single(pump01.Episodes);
        Assert.Equal(50, (episode.EndTs - episode.StartTs).TotalSeconds);
        Assert.True(episode.Qualifies);
        Assert.Empty(pump02.Episodes);
    }

    [Fact]
    public void RulesOnTheSameStream_DoNotShareState()
    {
        var strict = new RuleDefinition("RA", "strict", true, "temperature", null, "SustainedAbove", 89, null, null, 30);
        var loose = new RuleDefinition("RB", "loose", true, "temperature", null, "SustainedAbove", 80, null, null, 30);
        var readings = new[]
        {
            Reading(85, "2025-06-01T08:00:00Z", 1),
            Reading(90, "2025-06-01T08:00:20Z", 2),
            Reading(84, "2025-06-01T08:00:40Z", 3),
            Reading(70, "2025-06-01T08:00:50Z", 4),
        };

        var strictResult = ruleOperator.EvaluateStream("PUMP-01", "temperature", readings, strict);
        var looseResult = ruleOperator.EvaluateStream("PUMP-01", "temperature", readings, loose);

        var strictEpisode = Assert.Single(strictResult.Episodes);
        Assert.False(strictEpisode.Qualifies);
        var looseEpisode = Assert.Single(looseResult.Episodes);
        Assert.True(looseEpisode.Qualifies);
        Assert.Equal(3, looseResult.Violations.Count);
    }
}
