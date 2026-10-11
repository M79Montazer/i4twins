using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public sealed class RuleEvaluator(RuleOperatorRegistry operators)
{
    public ReadingEvaluation Evaluate(SensorReading reading, RuleSet rules)
    {
        var results = new List<RuleResult>();
        var evaluatedRules = 0;

        foreach (var rule in rules.ApplicableRules(reading))
        {
            if (!operators.TryGetStateless(rule.OperatorName, out var stateless))
            {
                continue;
            }

            evaluatedRules++;
            results.Add(stateless.EvaluateViolation(reading, rule) is { } reason
                ? RuleResult.Violation(rule.Id, rule.Name, reason)
                : RuleResult.NotViolated(rule.Id, rule.Name));
        }

        return new ReadingEvaluation(reading, Acceptable: results.All(r => !r.Violated), evaluatedRules, results);
    }
}
