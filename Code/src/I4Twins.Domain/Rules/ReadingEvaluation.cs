using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public sealed record ReadingEvaluation(
    SensorReading Reading,
    bool Acceptable,
    int EvaluatedRuleCount,
    IReadOnlyList<RuleResult> RuleResults)
{
    private IReadOnlyList<RuleViolation>? _violations;

    public IReadOnlyList<RuleViolation> Violations => _violations ??=
        RuleResults
            .Where(result => result.Violated)
            .Select(result => new RuleViolation(result.RuleId, result.RuleName, result.Reason!))
            .ToList();
}
