using System.Globalization;
using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules.Operators;


public abstract class ThresholdOperator(string name, string violationPhrase) : IStatelessRuleOperator
{
    public string Name => name;

    public IEnumerable<string> ValidateConfiguration(RawRule rule)
    {
        return RuleParameters.TryGetFiniteNumber(rule.Threshold, out _)
            ? []
            : ["threshold must be a finite number"];
    }

    public string? EvaluateViolation(SensorReading reading, RuleDefinition rule)
    {
        var threshold = rule.Threshold!.Value;
        return Violates(reading.Value, threshold)
            ? $"{reading.Metric} {Format(reading.Value)} is {violationPhrase} {Format(threshold)}"
            : null;
    }

    protected abstract bool Violates(double value, double threshold);

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
}

public sealed class GreaterThanOperator() : ThresholdOperator("GreaterThan", "greater than")
{
    protected override bool Violates(double value, double threshold) => value > threshold;
}

public sealed class GreaterThanOrEqualOperator() : ThresholdOperator("GreaterThanOrEqual", "greater than or equal to")
{
    protected override bool Violates(double value, double threshold) => value >= threshold;
}

public sealed class LessThanOperator() : ThresholdOperator("LessThan", "less than")
{
    protected override bool Violates(double value, double threshold) => value < threshold;
}

public sealed class LessThanOrEqualOperator() : ThresholdOperator("LessThanOrEqual", "less than or equal to")
{
    protected override bool Violates(double value, double threshold) => value <= threshold;
}

public sealed class EqualOperator() : ThresholdOperator("Equal", "equal to")
{
    protected override bool Violates(double value, double threshold) => value == threshold;
}
