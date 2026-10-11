using I4Twins.Domain.Rules;
using I4Twins.Domain.Readings;

namespace I4Twins.Application.Rules;

public sealed record RuleEvaluationOutcome(
    RuleEvaluationCounterSnapshot Counters,
    IReadOnlyList<ReadingEvaluation> Evaluations,
    IReadOnlyList<RuleEpisode> Episodes);

public sealed class RuleEvaluationService(RuleEvaluator evaluator, RuleOperatorRegistry operators)
{
    public RuleEvaluationOutcome Evaluate(IReadOnlyList<SensorReading> readings, RuleSet rules)
    {
        var ordered = EventTimeOrder.Sort(readings);
        var mutable = ordered.Select(r => new MutableEvaluation(r)).ToArray();
        var indexByReading = new Dictionary<ReadingKey, int>(mutable.Length);
        for (var i = 0; i < mutable.Length; i++)
        {
            indexByReading[ordered[i].Key] = i;
        }

        for (var i = 0; i < mutable.Length; i++)
        {
            var stateless = evaluator.Evaluate(ordered[i], rules);
            mutable[i].EvaluatedRules += stateless.EvaluatedRuleCount;
            mutable[i].Violations.AddRange(stateless.Violations);
        }

        var episodes = new List<RuleEpisode>();
        foreach (var stream in ordered.GroupBy(r => (r.DeviceId, r.Metric)))
        {
            var streamReadings = stream.ToArray();
            foreach (var rule in rules.ApplicableRules(stream.Key.DeviceId, stream.Key.Metric))
            {
                if (!operators.TryGetStateful(rule.OperatorName, out var stateful))
                {
                    continue;
                }

                var result = stateful.EvaluateStream(stream.Key.DeviceId, stream.Key.Metric, streamReadings, rule);
                episodes.AddRange(result.Episodes);
                foreach (var reading in streamReadings)
                {
                    mutable[indexByReading[reading.Key]].EvaluatedRules++;
                }

                foreach (var (key, violation) in result.Violations)
                {
                    mutable[indexByReading[key]].Violations.Add(violation);
                }
            }
        }

        var counters = new RuleEvaluationCounters();
        var evaluations = new List<ReadingEvaluation>(mutable.Length);
        foreach (var candidate in mutable)
        {
            var evaluation = candidate.Build();
            evaluations.Add(evaluation);
            counters.CountEvaluation(evaluation);
        }

        foreach (var episode in episodes)
        {
            counters.CountEpisode(episode);
        }

        return new RuleEvaluationOutcome(counters.Snapshot(), evaluations, episodes);
    }

    private sealed class MutableEvaluation(SensorReading reading)
    {
        public SensorReading Reading { get; } = reading;
        public int EvaluatedRules { get; set; }
        public List<RuleViolation> Violations { get; } = [];

        public ReadingEvaluation Build() =>
            new(Reading, Acceptable: Violations.Count == 0, EvaluatedRules, Violations);
    }
}
