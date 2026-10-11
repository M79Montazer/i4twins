using System.Globalization;
using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules.Operators;


public sealed class BetweenOperator : IStatelessRuleOperator
{
    public string Name => "Between";

    public IEnumerable<string> ValidateConfiguration(RawRule rule)
    {
        var minValid = RuleParameters.TryGetFiniteNumber(rule.Min, out var min);
        var maxValid = RuleParameters.TryGetFiniteNumber(rule.Max, out var max);
        if (!minValid)
        {
            yield return "min must be a finite number";
        }

        if (!maxValid)
        {
            yield return "max must be a finite number";
        }

        if (minValid && maxValid && min > max)
        {
            yield return "min must not be greater than max";
        }
    }

    public string? EvaluateViolation(SensorReading reading, RuleDefinition rule)
    {
        var min = rule.Min!.Value;
        var max = rule.Max!.Value;
        return reading.Value < min || reading.Value > max
            ? $"{reading.Metric} {Format(reading.Value)} is outside the range [{Format(min)}, {Format(max)}]"
            : null;
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
}
