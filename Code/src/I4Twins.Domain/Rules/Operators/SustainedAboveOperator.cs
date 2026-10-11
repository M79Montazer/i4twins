namespace I4Twins.Domain.Rules.Operators;


public sealed class SustainedAboveOperator : IRuleOperator
{
    public string Name => "SustainedAbove";

    public IEnumerable<string> ValidateConfiguration(RawRule rule)
    {
        if (!RuleParameters.TryGetFiniteNumber(rule.Threshold, out _))
        {
            yield return "threshold must be a finite number";
        }

        if (!RuleParameters.TryGetFiniteNumber(rule.DurationSeconds, out var duration))
        {
            yield return "durationSeconds must be a finite number";
        }
        else if (duration <= 0)
        {
            yield return "durationSeconds must be greater than 0";
        }
    }
}
