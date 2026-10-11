using I4Twins.Application;
using I4Twins.Application.Alerts;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Infrastructure.Input;
using I4Twins.Infrastructure.Rules;
using Xunit.Abstractions;

namespace I4Twins.IntegrationTests.Rules;

public class RealDataRuleEvaluationTests(ITestOutputHelper output)
{
    private ProcessingPipeline CreatePipeline() => new(
        new RuleSetService(
            new FileRuleSource(TestData.DataFile("rules.json")),
            new JsonRuleParser(),
            RuleOperatorRegistry.WithDefaultOperators()),
        new IngestionService(
            new FileLineSource(TestData.DataFile("readings.jsonl")),
            new JsonLineParser()),
        new RuleEvaluationService(
            new RuleEvaluator(RuleOperatorRegistry.WithDefaultOperators()),
            RuleOperatorRegistry.WithDefaultOperators()),
        new AlertService(),
        new NoopStore());

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
        var pipeline = CreatePipeline();

        var outcome = pipeline.Run();
        var ingestion = outcome.Ingestion;
        var evaluation = outcome.Evaluation;

        output.WriteLine($"Rules loaded:              {outcome.RulesLoaded}");
        output.WriteLine($"Stored readings:           {ingestion.StoredReadings}");
        output.WriteLine($"Rule evaluations:          {evaluation.EvaluationsPerformed}");
        output.WriteLine($"Acceptable readings:       {evaluation.AcceptableReadings}");
        output.WriteLine($"Unacceptable readings:     {evaluation.UnacceptableReadings}");
        output.WriteLine($"Rule violations:           {evaluation.RuleViolations}");
        foreach (var byRule in outcome.Evaluations.SelectMany(e => e.Violations)
                     .GroupBy(v => v.RuleId).OrderBy(g => g.Key))
        {
            output.WriteLine($"  violations by {byRule.Key}: {byRule.Count()}");
        }

        output.WriteLine($"Episodes detected:         {evaluation.EpisodesDetected} (qualifying: {evaluation.QualifyingEpisodes})");
        foreach (var episode in outcome.Episodes.Take(5))
        {
            output.WriteLine($"  e.g. {episode.RuleId}/{episode.DeviceId}/{episode.Metric}: {episode.StartTs:u} -> {episode.EndTs:u}, peak {episode.PeakValue}, qualifies: {episode.Qualifies}");
        }

        output.WriteLine($"Alerts raised:             {outcome.Alerts.AlertsRaised} (suppressed: {outcome.Alerts.AlertsSuppressed})");
        foreach (var alert in outcome.RaisedAlerts)
        {
            output.WriteLine($"  raised {alert.RuleId}/{alert.DeviceId}/{alert.Metric}: {alert.StartTs:u} -> {alert.EndTs:u}, peak {alert.PeakValue}");
        }
        foreach (var decision in outcome.AlertDecisions.Where(d => !d.Raised))
        {
            output.WriteLine($"  suppressed {decision.Episode.RuleId}: starts {decision.Episode.StartTs:u}, previous alert ended {decision.PreviousAlertEndTs:u}");
        }

        Assert.Equal(ingestion.StoredReadings, outcome.Evaluations.Count);
        Assert.Equal(ingestion.StoredReadings, evaluation.AcceptableReadings + evaluation.UnacceptableReadings);
        Assert.Equal(outcome.Evaluations.Sum(e => e.EvaluatedRuleCount), evaluation.EvaluationsPerformed);
        Assert.Equal(outcome.Evaluations.Sum(e => e.Violations.Count), evaluation.RuleViolations);
        Assert.All(outcome.Evaluations, e =>
            Assert.Equal(!e.Acceptable, e.Violations.Count > 0));

        var rules = outcome.Evaluations.SelectMany(e => e.Violations).Select(v => v.RuleId).Distinct();
        Assert.Equal(["R1", "R2", "R3"], rules.OrderBy(r => r));
        Assert.True(outcome.Episodes.Count > 0, "the stateful rule detects episodes on real data");

        Assert.True(evaluation.AcceptableReadings > 0);
        Assert.True(evaluation.UnacceptableReadings > 0);
        Assert.True(evaluation.RuleViolations > 0);

        Assert.Equal(1680, evaluation.EvaluationsPerformed);
        Assert.Equal(1942, evaluation.AcceptableReadings);
        Assert.Equal(161, evaluation.UnacceptableReadings);
        Assert.Equal(161, evaluation.RuleViolations);

        Assert.Equal(2, outcome.Alerts.QualifyingEpisodes);
        Assert.Equal(1, outcome.Alerts.AlertsRaised);
        Assert.Equal(1, outcome.Alerts.AlertsSuppressed);
        var raisedAlert = Assert.Single(outcome.RaisedAlerts);
        Assert.Equal("R2", raisedAlert.RuleId);
        Assert.Equal("PUMP-01", raisedAlert.DeviceId);
        Assert.Equal("temperature", raisedAlert.Metric);
        Assert.Equal(70.112, raisedAlert.PeakValue, precision: 3);
        Assert.Contains(outcome.AlertDecisions, d => !d.Raised && d.Episode.Qualifies);
    }

    [Fact]
    public void RealDataset_ProducesTheCompleteProcessingReport()
    {
        var outcome = CreatePipeline().Run();
        var report = ProcessingReport.Format(outcome);
        output.WriteLine(report);

        var requiredMetrics = new[]
        {
            "Total lines read:", "Parsed readings:", "Stored readings:", "Duplicates removed:",
            "Invalid records rejected:", "Rules loaded:", "Rule evaluations performed:",
            "Acceptable readings:", "Unacceptable readings:", "Rule violations:", "Alerts generated:",
        };
        Assert.All(requiredMetrics, label => Assert.Contains(label, report));

        Assert.Contains("Episodes detected:          4 (qualifying: 2)", report);
        Assert.Contains("Alerts generated:           1 (suppressed by cooldown: 1)", report);
        Assert.Contains("Total lines read:           2150", report);
        Assert.Contains("Stored readings:            2103", report);
    }
}
