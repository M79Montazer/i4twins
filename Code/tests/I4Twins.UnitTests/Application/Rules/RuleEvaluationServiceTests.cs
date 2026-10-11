using System.Globalization;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Application.Rules;

public class RuleEvaluationServiceTests
{
    private static readonly RuleOperatorRegistry Registry = RuleOperatorRegistry.WithDefaultOperators();
    private readonly RuleEvaluationService service = new(new RuleEvaluator(Registry), Registry);

    private static SensorReading Reading(string deviceId, double value, string ts, long seq, string metric = "temperature") =>
        new(deviceId, metric, DateTimeOffset.Parse(ts, styles: DateTimeStyles.AssumeUniversal), value, seq);

    private static RuleDefinition ThresholdRule(string id, double threshold) =>
        new(id, $"rule {id}", true, "temperature", null, "GreaterThan", threshold, null, null, null);

    private static RuleDefinition SustainedRule(string id, double threshold, double durationSeconds, string? deviceId = null) =>
        new(id, $"rule {id}", true, "temperature", deviceId, "SustainedAbove", threshold, null, null, durationSeconds);

    [Fact]
    public void OneReadingViolatingThreeRules_CountsOneUnacceptableAndThreeViolations()
    {
        var rules = new RuleSet([ThresholdRule("R1", 60), ThresholdRule("R2", 70), ThresholdRule("R3", 80)]);

        var outcome = service.Evaluate([Reading("PUMP-01", 90, "2025-06-01T08:00:00Z", 1)], rules);

        Assert.Equal(1, outcome.Counters.UnacceptableReadings);
        Assert.Equal(0, outcome.Counters.AcceptableReadings);
        Assert.Equal(3, outcome.Counters.RuleViolations);
        Assert.Equal(3, outcome.Counters.EvaluationsPerformed);
        Assert.Equal(3, outcome.Evaluations[0].Violations.Count);
    }

    [Fact]
    public void AcceptableReadings_AreCountedSeparately()
    {
        var rules = new RuleSet([ThresholdRule("R1", 60), ThresholdRule("R2", 70), ThresholdRule("R3", 80)]);

        var outcome = service.Evaluate([Reading("PUMP-01", 50, "2025-06-01T08:00:00Z", 1)], rules);

        Assert.Equal(1, outcome.Counters.AcceptableReadings);
        Assert.Equal(0, outcome.Counters.UnacceptableReadings);
        Assert.Equal(0, outcome.Counters.RuleViolations);
        Assert.Equal(3, outcome.Counters.EvaluationsPerformed);
    }

    [Fact]
    public void MixedBatch_CountersAreInternallyConsistent()
    {
        var rules = new RuleSet([ThresholdRule("R1", 60), ThresholdRule("R2", 70), ThresholdRule("R3", 80)]);

        var outcome = service.Evaluate(
        [
            Reading("PUMP-01", 50, "2025-06-01T08:00:00Z", 1),
            Reading("PUMP-01", 90, "2025-06-01T08:00:10Z", 2),
            Reading("PUMP-01", 65, "2025-06-01T08:00:20Z", 3),
        ], rules);

        var counters = outcome.Counters;
        Assert.Equal(1, counters.AcceptableReadings);
        Assert.Equal(2, counters.UnacceptableReadings);
        Assert.Equal(4, counters.RuleViolations);
        Assert.Equal(9, counters.EvaluationsPerformed);
        Assert.Equal(counters.AcceptableReadings + counters.UnacceptableReadings, outcome.Evaluations.Count);
    }

    [Fact]
    public void StatefulRule_ClassifiesRetroactively_AndCountsOneEvaluationPerReading()
    {
        var rules = new RuleSet([SustainedRule("R2", 69, 30, deviceId: "PUMP-01")]);

        var outcome = service.Evaluate(
        [
            Reading("PUMP-01", 70, "2025-06-01T08:00:00Z", 1),
            Reading("PUMP-01", 75, "2025-06-01T08:00:40Z", 2),
            Reading("PUMP-01", 69, "2025-06-01T08:01:00Z", 3),
        ], rules);

        Assert.Equal(2, outcome.Counters.UnacceptableReadings);
        Assert.Equal(1, outcome.Counters.AcceptableReadings);
        Assert.Equal(2, outcome.Counters.RuleViolations);
        Assert.Equal(3, outcome.Counters.EvaluationsPerformed);
        var episode = Assert.Single(outcome.Episodes);
        Assert.True(episode.Qualifies);
    }

    [Fact]
    public void OutOfOrderInput_ProducesTheSameResultAsSortedInput()
    {
        var rules = new RuleSet([SustainedRule("R2", 69, 30, deviceId: "PUMP-01")]);
        var sorted = new[]
        {
            Reading("PUMP-01", 70, "2025-06-01T08:00:00Z", 1),
            Reading("PUMP-01", 75, "2025-06-01T08:00:40Z", 2),
            Reading("PUMP-01", 69, "2025-06-01T08:01:00Z", 3),
        };

        var fromSorted = service.Evaluate(sorted, rules);
        var fromShuffled = service.Evaluate([sorted[2], sorted[0], sorted[1]], rules);

        Assert.Equal(fromSorted.Episodes, fromShuffled.Episodes);
        Assert.Equal(fromSorted.Counters, fromShuffled.Counters);
        Assert.Equal(
            fromSorted.Evaluations.Select(e => (e.Reading.Key, e.Acceptable, e.Violations.Count)),
            fromShuffled.Evaluations.Select(e => (e.Reading.Key, e.Acceptable, e.Violations.Count)));
    }

    [Fact]
    public void EvaluationsAreReturnedInEventTimeOrder()
    {
        var rules = new RuleSet([ThresholdRule("R1", 80)]);

        var outcome = service.Evaluate(
        [
            Reading("PUMP-01", 90, "2025-06-01T08:00:20Z", 3),
            Reading("PUMP-01", 90, "2025-06-01T08:00:00Z", 1),
            Reading("PUMP-01", 90, "2025-06-01T08:00:10Z", 2),
        ], rules);

        Assert.Equal([1, 2, 3], outcome.Evaluations.Select(e => e.Reading.Seq));
    }

    [Fact]
    public void EmptyInput_ProducesZeroCounters()
    {
        var rules = new RuleSet([ThresholdRule("R1", 80)]);

        var outcome = service.Evaluate([], rules);

        Assert.Equal(0, outcome.Counters.EvaluationsPerformed);
        Assert.Equal(0, outcome.Counters.AcceptableReadings);
        Assert.Equal(0, outcome.Counters.UnacceptableReadings);
        Assert.Equal(0, outcome.Counters.RuleViolations);
        Assert.Equal(0, outcome.Counters.EpisodesDetected);
        Assert.Empty(outcome.Evaluations);
        Assert.Empty(outcome.Episodes);
    }
}
