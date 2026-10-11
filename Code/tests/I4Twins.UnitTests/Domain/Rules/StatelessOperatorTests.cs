using System.Globalization;
using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Rules.Operators;

namespace I4Twins.UnitTests.Domain.Rules;

public class StatelessOperatorTests
{
    private static SensorReading Reading(double value, string metric = "temperature") =>
        new("PUMP-01", metric, DateTimeOffset.Parse("2025-06-01T08:00:00Z", styles: DateTimeStyles.AssumeUniversal), value, 1);

    private static RuleDefinition Rule(
        string operatorName,
        double? threshold = null,
        double? min = null,
        double? max = null) =>
        new("R1", "test rule", Enabled: true, "temperature", DeviceId: null, operatorName, threshold, min, max, null);

    private static string? Violation(IStatelessRuleOperator ruleOperator, double value, RuleDefinition rule) =>
        ruleOperator.EvaluateViolation(Reading(value), rule);

    [Fact]
    public void GreaterThan_ViolatesOnlyAboveThreshold()
    {
        var ruleOperator = new GreaterThanOperator();
        var rule = Rule("GreaterThan", threshold: 80);

        Assert.Null(Violation(ruleOperator, 79.99, rule));
        Assert.Null(Violation(ruleOperator, 80, rule));
        Assert.NotNull(Violation(ruleOperator, 80.01, rule));
    }

    [Fact]
    public void GreaterThanOrEqual_ViolatesAtAndAboveThreshold()
    {
        var ruleOperator = new GreaterThanOrEqualOperator();
        var rule = Rule("GreaterThanOrEqual", threshold: 80);

        Assert.Null(Violation(ruleOperator, 79.99, rule));
        Assert.NotNull(Violation(ruleOperator, 80, rule));
        Assert.NotNull(Violation(ruleOperator, 80.01, rule));
    }

    [Fact]
    public void LessThan_ViolatesOnlyBelowThreshold()
    {
        var ruleOperator = new LessThanOperator();
        var rule = Rule("LessThan", threshold: 10);

        Assert.NotNull(Violation(ruleOperator, 9.99, rule));
        Assert.Null(Violation(ruleOperator, 10, rule));
        Assert.Null(Violation(ruleOperator, 10.01, rule));
    }

    [Fact]
    public void LessThanOrEqual_ViolatesAtAndBelowThreshold()
    {
        var ruleOperator = new LessThanOrEqualOperator();
        var rule = Rule("LessThanOrEqual", threshold: 10);

        Assert.NotNull(Violation(ruleOperator, 9.99, rule));
        Assert.NotNull(Violation(ruleOperator, 10, rule));
        Assert.Null(Violation(ruleOperator, 10.01, rule));
    }

    [Fact]
    public void Equal_ViolatesOnlyExactEquality()
    {
        var ruleOperator = new EqualOperator();
        var rule = Rule("Equal", threshold: 80);

        Assert.NotNull(Violation(ruleOperator, 80, rule));
        Assert.Null(Violation(ruleOperator, 79.999, rule));
        Assert.Null(Violation(ruleOperator, 80.001, rule));
    }

    [Fact]
    public void Equal_UsesExactNumericEquality_FloatNoiseDoesNotMatch()
    {
        var ruleOperator = new EqualOperator();
        var rule = Rule("Equal", threshold: 0.3);

        Assert.Null(Violation(ruleOperator, 0.1 + 0.2, rule));
    }

    [Fact]
    public void Between_ViolatesOutsideTheInclusiveWindow()
    {
        var ruleOperator = new BetweenOperator();
        var rule = Rule("Between", min: 0, max: 7);

        Assert.NotNull(Violation(ruleOperator, -0.001, rule));
        Assert.Null(Violation(ruleOperator, 0, rule));
        Assert.Null(Violation(ruleOperator, 3.5, rule));
        Assert.Null(Violation(ruleOperator, 7, rule));
        Assert.NotNull(Violation(ruleOperator, 7.001, rule));
    }

    [Fact]
    public void Between_AllowsDegenerateSingleValueWindow()
    {
        var ruleOperator = new BetweenOperator();
        var rule = Rule("Between", min: 5, max: 5);

        Assert.Null(Violation(ruleOperator, 5, rule));
        Assert.NotNull(Violation(ruleOperator, 4.999, rule));
    }

    [Fact]
    public void ViolationReason_DescribesTheComparison()
    {
        var rule = Rule("GreaterThan", threshold: 80);

        var reason = new GreaterThanOperator().EvaluateViolation(Reading(90.5), rule);

        Assert.Equal("temperature 90.5 is greater than 80", reason);
    }

    [Fact]
    public void Between_ViolationReason_DescribesTheRange()
    {
        var rule = Rule("Between", min: 0, max: 7);

        var reason = new BetweenOperator().EvaluateViolation(Reading(7.5, metric: "vibration"), rule);

        Assert.Equal("vibration 7.5 is outside the range [0, 7]", reason);
    }
}
