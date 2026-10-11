using I4Twins.Domain.Rules;

namespace I4Twins.Application.Persistence;

public sealed record ProcessingBatch(
    IReadOnlyList<ReadingEvaluation> Evaluations,
    IReadOnlyList<RuleEpisode> Episodes,
    IReadOnlyList<Alert> RaisedAlerts);

public sealed record PersistenceResult(long NewReadings, long NewRuleResults, long NewEpisodes, long NewAlerts)
{
    public long NewRows => NewReadings + NewRuleResults + NewEpisodes + NewAlerts;

    public static PersistenceResult None { get; } = new(0, 0, 0, 0);
}

public interface IProcessingStore
{
    PersistenceResult Persist(ProcessingBatch batch);
}
