using I4Twins.Application.Alerts;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Persistence;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;

namespace I4Twins.Application;

public sealed record ProcessingOutcome(
    IngestionCounterSnapshot Ingestion,
    long RulesLoaded,
    RuleEvaluationCounterSnapshot Evaluation,
    IReadOnlyList<ReadingEvaluation> Evaluations,
    IReadOnlyList<RuleEpisode> Episodes,
    AlertCounterSnapshot Alerts,
    IReadOnlyList<Alert> RaisedAlerts,
    IReadOnlyList<AlertDecision> AlertDecisions,
    PersistenceResult Persistence);

public sealed class ProcessingPipeline(
    RuleSetService ruleSet,
    IngestionService ingestion,
    RuleEvaluationService evaluation,
    AlertService alerting,
    IProcessingStore store)
{
    public ProcessingOutcome Run()
    {
        var rules = ruleSet.Load();
        var ingested = ingestion.Run();
        var evaluated = evaluation.Evaluate(ingested.Readings, rules.RuleSet);
        var alerted = alerting.Evaluate(evaluated.Episodes);
        var persisted = store.Persist(new ProcessingBatch(evaluated.Evaluations, evaluated.Episodes, alerted.Alerts));
        return new ProcessingOutcome(
            ingested.Counters,
            rules.RulesLoaded,
            evaluated.Counters,
            evaluated.Evaluations,
            evaluated.Episodes,
            alerted.Counters,
            alerted.Alerts,
            alerted.Decisions,
            persisted);
    }
}
