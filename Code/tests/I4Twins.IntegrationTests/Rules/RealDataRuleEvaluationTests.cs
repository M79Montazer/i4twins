using I4Twins.Application;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Infrastructure.Input;
using I4Twins.Infrastructure.Rules;
using Xunit.Abstractions;

namespace I4Twins.IntegrationTests.Rules;

public class RealDataRuleEvaluationTests(ITestOutputHelper output)
{
    [Fact]
    public void RealRules_LoadAndValidate()
    {
        var service = new RuleSetService(
            new FileRuleSource(TestData.DataFile("rules.json")),
            new JsonRuleParser(),
            RuleOperatorRegistry.WithDefaultOperators());

        var outcome = service.Load();

        Assert.Equal(3, outcome.RulesLoaded);
        Assert.Equal(["R1", "R2", "R3"], outcome.RuleSet.Rules.Select(r => r.Id));
        var sustained = outcome.RuleSet.Rules.Single(r => r.Id == "R2");
        Assert.Equal("SustainedAbove", sustained.OperatorName);
        Assert.Equal("temperature", sustained.Metric);
        Assert.Equal("PUMP-01", sustained.DeviceId);
    }

    [Fact]
    public void RealDataset_EvaluationIsInternallyConsistent_WithoutCrashing()
    {
        var pipeline = new ProcessingPipeline(
            new RuleSetService(
                new FileRuleSource(TestData.DataFile("rules.json")),
                new JsonRuleParser(),
                RuleOperatorRegistry.WithDefaultOperators()),
            new IngestionService(
                new FileLineSource(TestData.DataFile("readings.jsonl")),
                new JsonLineParser()),
            new RuleEvaluationService(new RuleEvaluator(RuleOperatorRegistry.WithDefaultOperators())));

        var outcome = pipeline.Run();
        var ingestion = outcome.Ingestion;
        var evaluation = outcome.Evaluation;

        output.WriteLine($"Rules loaded:              {outcome.RulesLoaded}");
        output.WriteLine($"Stored readings:           {ingestion.StoredReadings}");
        output.WriteLine($"Rule evaluations:          {evaluation.EvaluationsPerformed}");
        output.WriteLine($"Acceptable readings:       {evaluation.AcceptableReadings}");
        output.WriteLine($"Unacceptable readings:     {evaluation.UnacceptableReadings}");
        output.WriteLine($"Rule violations:           {evaluation.RuleViolations}");
        foreach (var violation in outcome.Evaluations.SelectMany(e => e.Violations).Take(5))
        {
            output.WriteLine($"  e.g. {violation.RuleId}: {violation.Reason}");
        }

        Assert.Equal(ingestion.StoredReadings, outcome.Evaluations.Count);
        Assert.Equal(ingestion.StoredReadings, evaluation.AcceptableReadings + evaluation.UnacceptableReadings);
        Assert.Equal(outcome.Evaluations.Sum(e => e.EvaluatedRuleCount), evaluation.EvaluationsPerformed);
        Assert.Equal(outcome.Evaluations.Sum(e => e.Violations.Count), evaluation.RuleViolations);
        Assert.All(outcome.Evaluations, e =>
            Assert.Equal(!e.Acceptable, e.Violations.Count > 0));

        var rules = outcome.Evaluations.SelectMany(e => e.Violations).Select(v => v.RuleId).Distinct();
        Assert.DoesNotContain("R2", rules);

        Assert.True(evaluation.AcceptableReadings > 0);
        Assert.True(evaluation.UnacceptableReadings > 0);
        Assert.True(evaluation.RuleViolations > 0);

        Assert.Equal(1471, evaluation.EvaluationsPerformed);
        Assert.Equal(1960, evaluation.AcceptableReadings);
        Assert.Equal(143, evaluation.UnacceptableReadings);
        Assert.Equal(143, evaluation.RuleViolations);
    }
}
