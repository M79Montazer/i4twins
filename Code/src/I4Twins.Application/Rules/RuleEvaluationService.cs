using I4Twins.Domain.Rules;
using I4Twins.Domain.Readings;

namespace I4Twins.Application.Rules;

public sealed record RuleEvaluationOutcome(
    RuleEvaluationCounterSnapshot Counters,
    IReadOnlyList<ReadingEvaluation> Evaluations);

public sealed class RuleEvaluationService(RuleEvaluator evaluator)
{
    public RuleEvaluationOutcome Evaluate(IReadOnlyList<SensorReading> readings, RuleSet rules)
    {
        var counters = new RuleEvaluationCounters();
        var evaluations = new List<ReadingEvaluation>(readings.Count);

        foreach (var reading in readings)
        {
            var evaluation = evaluator.Evaluate(reading, rules);
            evaluations.Add(evaluation);
            counters.CountEvaluation(evaluation);
        }

        return new RuleEvaluationOutcome(counters.Snapshot(), evaluations);
    }
}
