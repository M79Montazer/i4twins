using I4Twins.Application.Ingestion;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;

namespace I4Twins.Application;

public sealed record ProcessingOutcome(
    IngestionCounterSnapshot Ingestion,
    long RulesLoaded,
    RuleEvaluationCounterSnapshot Evaluation,
    IReadOnlyList<ReadingEvaluation> Evaluations,
    IReadOnlyList<RuleEpisode> Episodes);

public sealed class ProcessingPipeline(RuleSetService ruleSet, IngestionService ingestion, RuleEvaluationService evaluation)
{
    public ProcessingOutcome Run()
    {
        var rules = ruleSet.Load();
        var ingested = ingestion.Run();
        var evaluated = evaluation.Evaluate(ingested.Readings, rules.RuleSet);
        return new ProcessingOutcome(ingested.Counters, rules.RulesLoaded, evaluated.Counters, evaluated.Evaluations, evaluated.Episodes);
    }
}
