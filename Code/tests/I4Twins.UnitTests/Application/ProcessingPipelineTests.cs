using System.Globalization;
using I4Twins.Application;
using I4Twins.Application.Alerts;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Rules;
using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;

namespace I4Twins.UnitTests.Application;

public class ProcessingPipelineTests
{
    private sealed class StubSource(params string[] lines) : ILineSource
    {
        public IEnumerable<string> ReadLines() => lines;
    }

    private sealed class StubParser(params (string Line, LineParseResult Result)[] script) : ILineParser
    {
        private readonly Dictionary<string, LineParseResult> _script = script.ToDictionary(p => p.Line, p => p.Result);

        public LineParseResult Parse(string line) => _script[line];
    }

    private sealed class StubRuleSource : IRuleSource
    {
        public string Read() => "unused";
    }

    private sealed class StubRuleParser(RawRule[] rules) : IRuleParser
    {
        public RuleDocumentParseResult Parse(string json) => new RuleDocumentParsed(rules);
    }

    private static LineParseResult Parsed(string ts, double value, long seq, string deviceId = "PUMP-01") =>
        new LineParsed(new RawReading(
            RawField.Of(deviceId),
            RawField.Of("temperature"),
            RawField.Of(ts),
            RawField.Of(value),
            RawField.Of(seq)));

    private static RawRule GlobalThresholdRule(double threshold) => new(
        RawField.Of("R1"),
        RawField.Of("high temperature"),
        RawField.Of(true),
        RawField.Of("temperature"),
        new RawField(RawFieldType.Missing),
        RawField.Of("GreaterThan"),
        RawField.Of(threshold),
        new RawField(RawFieldType.Missing),
        new RawField(RawFieldType.Missing),
        new RawField(RawFieldType.Missing));

    private static RawRule SustainedRule(double threshold, double durationSeconds) => new(
        RawField.Of("R2"),
        RawField.Of("sustained overheating"),
        RawField.Of(true),
        RawField.Of("temperature"),
        new RawField(RawFieldType.Missing),
        RawField.Of("SustainedAbove"),
        RawField.Of(threshold),
        new RawField(RawFieldType.Missing),
        new RawField(RawFieldType.Missing),
        RawField.Of(durationSeconds));

    private static ProcessingPipeline Pipeline(ILineSource source, ILineParser parser, RawRule[] rules) => new(
        new RuleSetService(new StubRuleSource(), new StubRuleParser(rules), RuleOperatorRegistry.WithDefaultOperators()),
        new IngestionService(source, parser),
        new RuleEvaluationService(new RuleEvaluator(RuleOperatorRegistry.WithDefaultOperators()), RuleOperatorRegistry.WithDefaultOperators()),
        new AlertService());

    [Fact]
    public void InvalidAndDuplicateRecords_AreNeverEvaluated()
    {
        var pipeline = Pipeline(
            new StubSource("malformed", "invalid", "first", "duplicate", "good"),
            new StubParser(
                ("malformed", new LineMalformed("invalid JSON")),
                ("invalid", Parsed("2025-06-01T08:00:01Z", 999, seq: 1, deviceId: "")),
                ("first", Parsed("2025-06-01T08:00:00Z", 100, seq: 1)),
                ("duplicate", Parsed("2025-06-01T08:00:00Z", 999, seq: 1)),
                ("good", Parsed("2025-06-01T08:00:02Z", 10, seq: 2))),
            [GlobalThresholdRule(50)]);

        var outcome = pipeline.Run();

        Assert.Equal(1, outcome.Ingestion.MalformedLines);
        Assert.Equal(1, outcome.Ingestion.ValidationRejected);
        Assert.Equal(1, outcome.Ingestion.DuplicatesRemoved);
        Assert.Equal(2, outcome.Ingestion.StoredReadings);
        Assert.Equal(2, outcome.Evaluations.Count);
        Assert.All(outcome.Evaluations, e => Assert.NotEqual(999, e.Reading.Value));
        Assert.Equal(1, outcome.Evaluation.UnacceptableReadings);
        Assert.Equal(1, outcome.Evaluation.AcceptableReadings);
        Assert.Equal(outcome.Ingestion.StoredReadings,
            outcome.Evaluation.AcceptableReadings + outcome.Evaluation.UnacceptableReadings);
    }

    [Fact]
    public void ManyReadingsInOneSustainedEpisode_ProduceExactlyOneAlert()
    {
        var lines = new List<string>();
        var script = new List<(string, LineParseResult)>();
        for (var i = 0; i < 10; i++)
        {
            var ts = $"2025-06-01T08:{i * 10 / 60:00}:{i * 10 % 60:00}Z";
            lines.Add($"reading-{i}");
            script.Add(($"reading-{i}", Parsed(ts, 85, seq: i + 1)));
        }

        var pipeline = Pipeline(
            new StubSource([.. lines]),
            new StubParser([.. script]),
            [SustainedRule(80, 30)]);

        var outcome = pipeline.Run();

        Assert.Single(outcome.RaisedAlerts);
        Assert.Equal(1, outcome.Alerts.AlertsRaised);
        Assert.Equal(10, outcome.Evaluation.UnacceptableReadings);
        var alert = outcome.RaisedAlerts[0];
        Assert.Equal("2025-06-01T08:00:00Z", alert.StartTs.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        Assert.Equal("2025-06-01T08:01:30Z", alert.EndTs.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        Assert.Equal(85, alert.PeakValue);
    }

    [Fact]
    public void InvalidRuleConfiguration_FailsTheRunLoudly()
    {
        var pipeline = Pipeline(
            new StubSource("first"),
            new StubParser(("first", Parsed("2025-06-01T08:00:00Z", 100, seq: 1))),
            [GlobalThresholdRule(50) with { Operator = RawField.Of("NoSuchOperator") }]);

        Assert.Throws<RuleConfigurationException>(pipeline.Run);
    }
}
