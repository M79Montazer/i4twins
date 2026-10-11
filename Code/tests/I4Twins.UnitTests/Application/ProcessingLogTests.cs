using System.Globalization;
using I4Twins.Application;
using I4Twins.Application.Alerts;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Persistence;
using I4Twins.Application.Rules;
using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;

namespace I4Twins.UnitTests.Application;

public class ProcessingLogTests
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

    private sealed class NoopStore : IProcessingStore
    {
        public PersistenceResult Persist(ProcessingBatch batch) => PersistenceResult.None;
    }

    private sealed class RecordingLog : IProcessingLog
    {
        public List<(string Level, string Message)> Entries { get; } = [];

        public void Debug(string message) => Entries.Add(("Debug", message));
        public void Information(string message) => Entries.Add(("Information", message));
        public void Warning(string message) => Entries.Add(("Warning", message));
        public void Error(string message) => Entries.Add(("Error", message));
    }

    private static LineParseResult Parsed(string ts, double value, long seq, string deviceId = "PUMP-01") =>
        new LineParsed(new RawReading(
            RawField.Of(deviceId),
            RawField.Of("temperature"),
            RawField.Of(ts),
            RawField.Of(value),
            RawField.Of(seq)));

    private static RawRule SustainedRule() => new(
        RawField.Of("R2"),
        RawField.Of("sustained overheating"),
        RawField.Of(true),
        RawField.Of("temperature"),
        new RawField(RawFieldType.Missing),
        RawField.Of("SustainedAbove"),
        RawField.Of(80),
        new RawField(RawFieldType.Missing),
        new RawField(RawFieldType.Missing),
        RawField.Of(30));

    [Fact]
    public void SignificantEvents_AreLoggedAtAppropriateLevels()
    {
        var log = new RecordingLog();
        var pipeline = new ProcessingPipeline(
            new RuleSetService(new StubRuleSource(), new StubRuleParser([SustainedRule()]), RuleOperatorRegistry.WithDefaultOperators()),
            new IngestionService(
                new StubSource("malformed", "invalid", "dup-first", "dup-second",
                    "r1", "r2", "r3", "r4", "r5", "r6", "r7"),
                new StubParser(
                    ("malformed", new LineMalformed("invalid JSON")),
                    ("invalid", Parsed("2025-06-01T08:00:01Z", 99, seq: 50, deviceId: "")),
                    ("dup-first", Parsed("2025-06-01T08:09:00Z", 10, seq: 7)),
                    ("dup-second", Parsed("2025-06-01T08:09:00Z", 99, seq: 7)),
                    ("r1", Parsed("2025-06-01T08:00:00Z", 85, seq: 1)),
                    ("r2", Parsed("2025-06-01T08:00:20Z", 85, seq: 2)),
                    ("r3", Parsed("2025-06-01T08:00:40Z", 85, seq: 3)),
                    ("r4", Parsed("2025-06-01T08:01:00Z", 70, seq: 4)),
                    ("r5", Parsed("2025-06-01T08:04:00Z", 85, seq: 5)),
                    ("r6", Parsed("2025-06-01T08:05:00Z", 70, seq: 6)),
                    ("r7", Parsed("2025-06-01T08:09:00Z", 10, seq: 8))),
                log),
            new RuleEvaluationService(new RuleEvaluator(RuleOperatorRegistry.WithDefaultOperators()), RuleOperatorRegistry.WithDefaultOperators(), log),
            new AlertService(new CooldownPolicy(), log),
            new NoopStore(),
            log);

        pipeline.Run();

        Assert.Contains(log.Entries, e => e.Level == "Information" && e.Message.Contains("loaded 1 rules"));
        Assert.Contains(log.Entries, e => e.Level == "Warning" && e.Message.Contains("line 1: rejected malformed record"));
        Assert.Contains(log.Entries, e => e.Level == "Warning" && e.Message.Contains("line 2: rejected invalid record"));
        Assert.Contains(log.Entries, e => e.Level == "Debug" && e.Message.Contains("duplicate reading dropped"));
        Assert.Equal(2, log.Entries.Count(e => e.Level == "Warning" && e.Message.Contains("rule R2 violated")));
        Assert.Single(log.Entries, e => e.Level == "Information" && e.Message.Contains("alert raised"));
        Assert.Single(log.Entries, e => e.Level == "Information" && e.Message.Contains("alert suppressed"));
        Assert.Contains(log.Entries, e => e.Level == "Information" && e.Message.Contains("persisted 0 new rows"));
    }

    [Fact]
    public void WithoutALog_ThePipelineRunsSilently()
    {
        var pipeline = new ProcessingPipeline(
            new RuleSetService(new StubRuleSource(), new StubRuleParser([SustainedRule()]), RuleOperatorRegistry.WithDefaultOperators()),
            new IngestionService(new StubSource("r1"), new StubParser(("r1", Parsed("2025-06-01T08:00:00Z", 85, seq: 1)))),
            new RuleEvaluationService(new RuleEvaluator(RuleOperatorRegistry.WithDefaultOperators()), RuleOperatorRegistry.WithDefaultOperators()),
            new AlertService(),
            new NoopStore());

        var outcome = pipeline.Run();

        Assert.Equal(1, outcome.Ingestion.StoredReadings);
    }
}
