using System.Globalization;
using I4Twins.Application;
using I4Twins.Application.Alerts;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Persistence;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Application;

public class ProcessingReportTests
{
    private static readonly DateTimeOffset Ts = DateTimeOffset.Parse("2025-06-01T08:00:00Z", styles: DateTimeStyles.AssumeUniversal);

    private static ProcessingOutcome Outcome() => new(
        new IngestionCounterSnapshot(TotalLines: 100, ParsedReadings: 97, MalformedLines: 1, ValidationRejected: 2, DuplicatesRemoved: 4, StoredReadings: 90),
        RulesLoaded: 3,
        new RuleEvaluationCounterSnapshot(
            EvaluationsPerformed: 150,
            AcceptableReadings: 88,
            UnacceptableReadings: 2,
            RuleViolations: 3,
            EpisodesDetected: 3,
            QualifyingEpisodes: 2),
        [],
        [],
        new AlertCounterSnapshot(QualifyingEpisodes: 2, AlertsRaised: 1, AlertsSuppressed: 1),
        [new Alert("R2", "PUMP-01", "temperature", Ts, Ts.AddMinutes(1), 90)],
        [],
        new PersistenceResult(90, 150, 3, 1));

    [Fact]
    public void Report_ContainsEveryRequiredMetric()
    {
        var report = ProcessingReport.Format(Outcome());

        var required = new[]
        {
            "Total lines read:", "Parsed readings:", "Stored readings:", "Duplicates removed:",
            "Invalid records rejected:", "Rules loaded:", "Rule evaluations performed:",
            "Acceptable readings:", "Unacceptable readings:", "Rule violations:", "Alerts generated:",
        };
        Assert.All(required, label => Assert.Contains(label, report));
    }

    [Fact]
    public void Report_ShowsTheMeasuredCounterValues()
    {
        var report = ProcessingReport.Format(Outcome());

        Assert.Contains("Total lines read:           100", report);
        Assert.Contains("Stored readings:            90", report);
        Assert.Contains("Invalid records rejected:   3 (malformed: 1, invalid fields: 2)", report);
        Assert.Contains("Rule evaluations performed: 150", report);
        Assert.Contains("Unacceptable readings:      2", report);
        Assert.Contains("Rule violations:            3", report);
        Assert.Contains("Alerts generated:           1 (suppressed by cooldown: 1)", report);
    }

    [Fact]
    public void Report_DistinguishesDetectedEpisodes_RaisedAlerts_AndSuppressedAlerts()
    {
        var report = ProcessingReport.Format(Outcome());

        Assert.Contains("Episodes detected:          3 (qualifying: 2)", report);
        Assert.Contains("Alerts generated:           1 (suppressed by cooldown: 1)", report);
    }

    [Fact]
    public void Report_DistinguishesRowsInsertedThisRun_FromPersistedTotals()
    {
        var report = ProcessingReport.Format(Outcome());

        Assert.Contains("Rows inserted this run:     244", report);
    }
}
