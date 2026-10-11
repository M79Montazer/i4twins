using I4Twins.Application.Ingestion;
using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Application;

public class IngestionServiceTests
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

    private static LineParseResult Parsed(
        string deviceId = "PUMP-01",
        string metric = "temperature",
        string ts = "2025-06-01T08:00:00Z",
        double value = 67.21,
        long seq = 1199)
    {
        var raw = new RawReading(
            DeviceId: RawField.Of(deviceId),
            Metric: RawField.Of(metric),
            Timestamp: RawField.Of(ts),
            Value: RawField.Of(value),
            Seq: RawField.Of(seq));
        return new LineParsed(raw);
    }

    private static SensorReading ReadingOf(IngestionOutcome outcome, int index) => outcome.Readings[index];

    [Fact]
    public void MalformedLine_IsCounted_AndProcessingContinues()
    {
        var outcome = new IngestionService(
            new StubSource("{not json", "valid-line"),
            new StubParser(
                ("{not json", new LineMalformed("invalid JSON")),
                ("valid-line", Parsed())))
            .Run();

        Assert.Equal(2, outcome.Counters.TotalLines);
        Assert.Equal(1, outcome.Counters.MalformedLines);
        Assert.Equal(1, outcome.Counters.StoredReadings);
    }

    [Fact]
    public void SemanticallyInvalidRecord_IsCounted_AndNeverReachesOutput()
    {
        var outcome = new IngestionService(
            new StubSource("bad-record", "good-record"),
            new StubParser(
                ("bad-record", Parsed(deviceId: "")),
                ("good-record", Parsed(seq: 1200))))
            .Run();

        Assert.Equal(1, outcome.Counters.ValidationRejected);
        Assert.Equal(0, outcome.Counters.MalformedLines);
        Assert.Single(outcome.Readings);
        Assert.All(outcome.Readings, r => Assert.NotEqual(string.Empty, r.DeviceId));
    }

    [Fact]
    public void DuplicateIdentityWithDifferentValue_FirstOccurrenceWins()
    {
        var outcome = new IngestionService(
            new StubSource("first", "second"),
            new StubParser(
                ("first", Parsed(value: 81.2)),
                ("second", Parsed(value: 82.4)))) 
            .Run();

        Assert.Equal(1, outcome.Counters.DuplicatesRemoved);
        Assert.Single(outcome.Readings);
        Assert.Equal(81.2, ReadingOf(outcome, 0).Value);
    }

    [Fact]
    public void SameTimestampWithDifferentSeq_BothAreStored()
    {
        var outcome = new IngestionService(
            new StubSource("a", "b"),
            new StubParser(
                ("a", Parsed(seq: 1199, ts: "2025-06-01T08:00:00Z")),
                ("b", Parsed(seq: 1200, ts: "2025-06-01T08:00:00Z"))))
            .Run();

        Assert.Equal(2, outcome.Counters.StoredReadings);
        Assert.Equal(0, outcome.Counters.DuplicatesRemoved);
    }

    [Fact]
    public void BlankLine_CountsTowardTotal_ButIsOtherwiseIgnored()
    {
        var outcome = new IngestionService(
            new StubSource("", "   ", "valid"),
            new StubParser(("valid", Parsed())))
            .Run();

        Assert.Equal(3, outcome.Counters.TotalLines);
        Assert.Equal(1, outcome.Counters.StoredReadings);
        Assert.Equal(0, outcome.Counters.InvalidRecords);
    }

    [Fact]
    public void Counters_AreInternallyConsistent()
    {

        var outcome = new IngestionService(
            new StubSource("malformed", "invalid", "duplicate", "repeat"),
            new StubParser(
                ("malformed", new LineMalformed("invalid JSON")),
                ("invalid", Parsed(deviceId: "")),
                ("duplicate", Parsed(value: 1)),
                ("repeat", Parsed(value: 2))))
            .Run();

        var c = outcome.Counters;
        Assert.Equal(c.TotalLines, c.StoredReadings + c.DuplicatesRemoved + c.InvalidRecords);
        Assert.Equal(4, c.TotalLines);
        Assert.Equal(1, c.StoredReadings);
        Assert.Equal(1, c.DuplicatesRemoved);
        Assert.Equal(2, c.InvalidRecords);
        Assert.Equal(1, c.MalformedLines);
        Assert.Equal(1, c.ValidationRejected);
    }

    [Fact]
    public void Readings_AreReturnedInFileOrder()
    {
        var outcome = new IngestionService(
            new StubSource("later", "earlier"),
            new StubParser(
                ("later", Parsed(ts: "2025-06-01T08:02:00Z", seq: 1)),
                ("earlier", Parsed(ts: "2025-06-01T08:01:00Z", seq: 2))))
            .Run();


        Assert.Equal(2, outcome.Readings.Count);
        Assert.Equal(1, outcome.Readings[0].Seq);
        Assert.Equal(2, outcome.Readings[1].Seq);
    }
}
