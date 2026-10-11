using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public sealed class RuleSet(IReadOnlyList<RuleDefinition> rules)
{
    private readonly Dictionary<string, List<RuleDefinition>> _byMetric = rules
        .GroupBy(rule => rule.Metric, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.ToList());

    public IReadOnlyList<RuleDefinition> Rules { get; } = rules;

    public IEnumerable<RuleDefinition> ApplicableRules(SensorReading reading) =>
        _byMetric.TryGetValue(reading.Metric, out var candidates)
            ? candidates.Where(rule => rule.AppliesTo(reading))
            : [];
}
