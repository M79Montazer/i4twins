using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public sealed record ReadingEvaluation(
    SensorReading Reading,
    bool Acceptable,
    int EvaluatedRuleCount,
    IReadOnlyList<RuleViolation> Violations);
