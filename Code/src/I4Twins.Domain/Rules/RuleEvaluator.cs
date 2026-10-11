using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public sealed class RuleEvaluator(RuleOperatorRegistry operators)
{
    public ReadingEvaluation Evaluate(SensorReading reading, RuleSet rules)
    {
        var violations = new List<RuleViolation>();
        var evaluatedRules = 0;

        foreach (var rule in rules.ApplicableRules(reading))
        {
            if (!operators.TryGetStateless(rule.OperatorName, out var stateless))
            {
                continue;
            }

            evaluatedRules++;
            if (stateless.EvaluateViolation(reading, rule) is { } reason)
            {
                violations.Add(new RuleViolation(rule.Id, rule.Name, reason));
            }
        }

        return new ReadingEvaluation(reading, Acceptable: violations.Count == 0, evaluatedRules, violations);
    }
}
