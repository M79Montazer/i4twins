using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public sealed record StatefulStreamResult(
    IReadOnlyList<RuleEpisode> Episodes,
    IReadOnlyDictionary<ReadingKey, RuleViolation> Violations)
{
    public static StatefulStreamResult Empty { get; } = new([], new Dictionary<ReadingKey, RuleViolation>());
}
