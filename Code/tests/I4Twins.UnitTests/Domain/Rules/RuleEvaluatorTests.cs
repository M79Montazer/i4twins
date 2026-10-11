using System.Globalization;
using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;

namespace I4Twins.UnitTests.Domain.Rules;

public class RuleEvaluatorTests
{
    private readonly RuleEvaluator evaluator = new(RuleOperatorRegistry.WithDefaultOperators());

    private static SensorReading Reading(double value, string metric = "temperature", long seq = 1) =>
        new("PUMP-01", metric, DateTimeOffset.Parse("2025-06-01T08:00:00Z", styles: DateTimeStyles.AssumeUniversal), value, seq);

    private static RuleDefinition ThresholdRule(
        string id, double threshold, bool enabled = true, string metric = "temperature", string? deviceId = null) =>
        new(id, $"rule {id}", enabled, metric, deviceId, "GreaterThan", threshold, null, null, null);

    private static RuleSet RuleSetOf(params RuleDefinition[] rules) => new(rules);

    [Fact]
    public void ReadingViolatingMultipleRules_ReportsEveryViolation()
    {
        var rules = RuleSetOf(ThresholdRule("R1", 80), ThresholdRule("R2", 85));

        var evaluation = evaluator.Evaluate(Reading(90), rules);

        Assert.False(evaluation.Acceptable);
        Assert.Equal(2, evaluation.Violations.Count);
        Assert.Equal(["R1", "R2"], evaluation.Violations.Select(v => v.RuleId));
    }

    [Fact]
    public void Violation_CarriesRuleIdentityAndMeaningfulReason()
    {
        var rules = RuleSetOf(ThresholdRule("R1", 80));

        var evaluation = evaluator.Evaluate(Reading(90), rules);

        var violation = Assert.Single(evaluation.Violations);
        Assert.Equal("R1", violation.RuleId);
        Assert.Equal("rule R1", violation.RuleName);
        Assert.False(string.IsNullOrWhiteSpace(violation.Reason));
    }

    [Fact]
    public void NoApplicableRule_IsAcceptable()
    {
        var rules = RuleSetOf(ThresholdRule("R1", 80, metric: "temperature"));

        var evaluation = evaluator.Evaluate(Reading(50, metric: "pressure"), rules);

        Assert.True(evaluation.Acceptable);
        Assert.Empty(evaluation.Violations);
        Assert.Equal(0, evaluation.EvaluatedRuleCount);
    }

    [Fact]
    public void ApplicableRuleNotViolated_IsAcceptable()
    {
        var rules = RuleSetOf(ThresholdRule("R1", 80));

        var evaluation = evaluator.Evaluate(Reading(70), rules);

        Assert.True(evaluation.Acceptable);
        Assert.Empty(evaluation.Violations);
        Assert.Equal(1, evaluation.EvaluatedRuleCount);
    }

    [Fact]
    public void DisabledRule_IsIgnored()
    {
        var rules = RuleSetOf(ThresholdRule("R1", 50, enabled: false));

        var evaluation = evaluator.Evaluate(Reading(90), rules);

        Assert.True(evaluation.Acceptable);
        Assert.Equal(0, evaluation.EvaluatedRuleCount);
    }

    [Fact]
    public void DeviceSpecificRule_DoesNotAffectOtherDevices()
    {
        var rules = RuleSetOf(ThresholdRule("R1", 50, deviceId: "PUMP-02"));

        var evaluation = evaluator.Evaluate(Reading(90), rules);

        Assert.True(evaluation.Acceptable);
        Assert.Equal(0, evaluation.EvaluatedRuleCount);
    }

    [Fact]
    public void StatefulRule_IsSkippedByTheStatelessPass()
    {
        var stateful = new RuleDefinition(
            "R2", "sustained", true, "temperature", null, "SustainedAbove", 69, null, null, 30);

        var evaluation = evaluator.Evaluate(Reading(90), RuleSetOf(ThresholdRule("R1", 80), stateful));

        Assert.False(evaluation.Acceptable);
        Assert.Equal(["R1"], evaluation.Violations.Select(v => v.RuleId));
        Assert.Equal(1, evaluation.EvaluatedRuleCount);
    }
}
