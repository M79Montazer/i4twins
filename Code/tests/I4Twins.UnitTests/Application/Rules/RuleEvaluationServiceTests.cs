using System.Globalization;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Application.Rules;

public class RuleEvaluationServiceTests
{
    private readonly RuleEvaluationService service = new(new RuleEvaluator(RuleOperatorRegistry.WithDefaultOperators()));

    private static SensorReading Reading(double value, long seq = 1) =>
        new("PUMP-01", "temperature", DateTimeOffset.Parse("2025-06-01T08:00:00Z", styles: DateTimeStyles.AssumeUniversal), value, seq);

    private static RuleSet TemperatureRules(params RuleDefinition[] rules) => new(rules);

    private static RuleDefinition ThresholdRule(string id, double threshold) =>
        new(id, $"rule {id}", true, "temperature", null, "GreaterThan", threshold, null, null, null);

    [Fact]
    public void OneReadingViolatingThreeRules_CountsOneUnacceptableAndThreeViolations()
    {
        var rules = TemperatureRules(ThresholdRule("R1", 60), ThresholdRule("R2", 70), ThresholdRule("R3", 80));

        var outcome = service.Evaluate([Reading(90)], rules);

        Assert.Equal(1, outcome.Counters.UnacceptableReadings);
        Assert.Equal(0, outcome.Counters.AcceptableReadings);
        Assert.Equal(3, outcome.Counters.RuleViolations);
        Assert.Equal(3, outcome.Counters.EvaluationsPerformed);
        Assert.Equal(3, outcome.Evaluations[0].Violations.Count);
    }

    [Fact]
    public void AcceptableReadings_AreCountedSeparately()
    {
        var rules = TemperatureRules(ThresholdRule("R1", 60), ThresholdRule("R2", 70), ThresholdRule("R3", 80));

        var outcome = service.Evaluate([Reading(50)], rules);

        Assert.Equal(1, outcome.Counters.AcceptableReadings);
        Assert.Equal(0, outcome.Counters.UnacceptableReadings);
        Assert.Equal(0, outcome.Counters.RuleViolations);
        Assert.Equal(3, outcome.Counters.EvaluationsPerformed);
    }

    [Fact]
    public void MixedBatch_CountersAreInternallyConsistent()
    {
        var rules = TemperatureRules(ThresholdRule("R1", 60), ThresholdRule("R2", 70), ThresholdRule("R3", 80));

        var outcome = service.Evaluate([Reading(50, seq: 1), Reading(90, seq: 2), Reading(65, seq: 3)], rules);

        var counters = outcome.Counters;
        Assert.Equal(1, counters.AcceptableReadings);
        Assert.Equal(2, counters.UnacceptableReadings);
        Assert.Equal(4, counters.RuleViolations);
        Assert.Equal(9, counters.EvaluationsPerformed);
        Assert.Equal(counters.AcceptableReadings + counters.UnacceptableReadings, outcome.Evaluations.Count);
    }

    [Fact]
    public void StatefulRules_DoNotContributeEvaluationsOrViolationsYet()
    {
        var stateful = new RuleDefinition(
            "RS", "sustained", true, "temperature", null, "SustainedAbove", 69, null, null, 30);
        var rules = TemperatureRules(ThresholdRule("R1", 80), stateful);

        var outcome = service.Evaluate([Reading(90)], rules);

        Assert.Equal(1, outcome.Counters.EvaluationsPerformed);
        var violation = Assert.Single(outcome.Evaluations[0].Violations);
        Assert.Equal("R1", violation.RuleId);
    }

    [Fact]
    public void EmptyInput_ProducesZeroCounters()
    {
        var rules = TemperatureRules(ThresholdRule("R1", 80));

        var outcome = service.Evaluate([], rules);

        Assert.Equal(0, outcome.Counters.EvaluationsPerformed);
        Assert.Equal(0, outcome.Counters.AcceptableReadings);
        Assert.Equal(0, outcome.Counters.UnacceptableReadings);
        Assert.Equal(0, outcome.Counters.RuleViolations);
        Assert.Empty(outcome.Evaluations);
    }
}
