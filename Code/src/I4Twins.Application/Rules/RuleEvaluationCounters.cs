using I4Twins.Domain.Rules;

namespace I4Twins.Application.Rules;

public sealed record RuleEvaluationCounterSnapshot(
    long EvaluationsPerformed,
    long AcceptableReadings,
    long UnacceptableReadings,
    long RuleViolations,
    long EpisodesDetected,
    long QualifyingEpisodes);

public sealed class RuleEvaluationCounters
{
    public long EvaluationsPerformed { get; private set; }
    public long AcceptableReadings { get; private set; }
    public long UnacceptableReadings { get; private set; }
    public long RuleViolations { get; private set; }
    public long EpisodesDetected { get; private set; }
    public long QualifyingEpisodes { get; private set; }

    public void CountEvaluation(ReadingEvaluation evaluation)
    {
        EvaluationsPerformed += evaluation.EvaluatedRuleCount;
        RuleViolations += evaluation.Violations.Count;
        if (evaluation.Acceptable)
        {
            AcceptableReadings++;
        }
        else
        {
            UnacceptableReadings++;
        }
    }

    public void CountEpisode(RuleEpisode episode)
    {
        EpisodesDetected++;
        if (episode.Qualifies)
        {
            QualifyingEpisodes++;
        }
    }

    public RuleEvaluationCounterSnapshot Snapshot() => new(
        EvaluationsPerformed,
        AcceptableReadings,
        UnacceptableReadings,
        RuleViolations,
        EpisodesDetected,
        QualifyingEpisodes);
}
