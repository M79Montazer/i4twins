using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Rules.Operators;

namespace I4Twins.UnitTests.Domain.Rules;

public class RuleOperatorRegistryTests
{
    private static readonly string[] StatelessOperatorNames =
    [
        "GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual", "Equal", "Between",
    ];

    [Fact]
    public void DefaultRegistry_KnowsEveryDefaultOperator()
    {
        var registry = RuleOperatorRegistry.WithDefaultOperators();

        Assert.All(StatelessOperatorNames, name => Assert.True(registry.IsKnown(name), name));
        Assert.True(registry.IsKnown("SustainedAbove"));
    }

    [Fact]
    public void DefaultRegistry_ResolvesStatelessOperators()
    {
        var registry = RuleOperatorRegistry.WithDefaultOperators();

        Assert.All(StatelessOperatorNames, name =>
            Assert.True(registry.TryGetStateless(name, out _), name));
    }

    [Fact]
    public void SustainedAbove_IsKnownButNotStateless()
    {
        var registry = RuleOperatorRegistry.WithDefaultOperators();

        Assert.True(registry.IsKnown("SustainedAbove"));
        Assert.False(registry.TryGetStateless("SustainedAbove", out _));
    }

    [Fact]
    public void UnknownOperator_IsNotKnown()
    {
        var registry = RuleOperatorRegistry.WithDefaultOperators();

        Assert.False(registry.IsKnown("NoSuchOperator"));
        Assert.False(registry.TryGetStateless("NoSuchOperator", out _));
    }

    [Fact]
    public void OperatorNames_MatchCaseSensitively()
    {
        var registry = RuleOperatorRegistry.WithDefaultOperators();

        Assert.False(registry.IsKnown("greaterthan"));
    }

    [Fact]
    public void RegisteringADuplicateName_Throws()
    {
        var registry = RuleOperatorRegistry.WithDefaultOperators();

        Assert.Throws<InvalidOperationException>(() => registry.Register(new EqualOperator()));
    }

    [Fact]
    public void NewlyRegisteredOperator_IsUsableByTheEvaluator()
    {
        var registry = RuleOperatorRegistry.WithDefaultOperators();
        registry.Register(new NotEqualOperator());

        var evaluator = new RuleEvaluator(registry);
        var rules = new RuleSet(
        [
            new RuleDefinition("R9", "not equal", true, "temperature", null, "NotEqual", 80, null, null, null),
        ]);
        var reading = new SensorReading(
            "PUMP-01", "temperature", DateTimeOffset.Parse("2025-06-01T08:00:00Z"), 90, 1);

        var evaluation = evaluator.Evaluate(reading, rules);

        var violation = Assert.Single(evaluation.Violations);
        Assert.Equal("R9", violation.RuleId);
    }

    private sealed class NotEqualOperator : IStatelessRuleOperator
    {
        public string Name => "NotEqual";

        public IEnumerable<string> ValidateConfiguration(RawRule rule) =>
            rule.Threshold.Type is RawFieldType.Number or RawFieldType.Integer
                ? []
                : ["threshold must be a finite number"];

        public string? EvaluateViolation(SensorReading reading, RuleDefinition rule) =>
            reading.Value == rule.Threshold!.Value ? null : $"{reading.Metric} {reading.Value} is not equal to {rule.Threshold}";
    }
}
