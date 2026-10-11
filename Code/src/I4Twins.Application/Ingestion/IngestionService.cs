using I4Twins.Domain.Readings;

namespace I4Twins.Application.Ingestion;

public sealed record IngestionOutcome(IngestionCounterSnapshot Counters, IReadOnlyList<SensorReading> Readings);


public sealed class IngestionService(ILineSource source, ILineParser parser, IProcessingLog? log = null)
{
    public IngestionOutcome Run()
    {
        var counters = new IngestionCounters();
        var duplicates = new DuplicateFilter();
        var stored = new List<SensorReading>();
        var lineNumber = 0;

        foreach (var line in source.ReadLines())
        {
            lineNumber++;
            counters.CountTotalLine();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parseResult = parser.Parse(line);
            if (parseResult is LineMalformed malformed)
            {
                counters.CountMalformed();
                log?.Warning($"line {lineNumber}: rejected malformed record ({malformed.Reason})");
                continue;
            }

            counters.CountParsed();
            var raw = ((LineParsed)parseResult).Reading;

            var validation = ReadingValidator.Validate(raw);
            if (!validation.IsValid)
            {
                counters.CountValidationRejected();
                log?.Warning($"line {lineNumber}: rejected invalid record ({validation.Error})");
                continue;
            }

            var reading = validation.Reading!;
            if (duplicates.IsDuplicate(reading))
            {
                counters.CountDuplicate();
                log?.Debug($"line {lineNumber}: duplicate reading dropped, first occurrence wins ({reading.Key.DeviceId}/{reading.Key.Metric} seq {reading.Key.Seq})");
                continue;
            }

            stored.Add(reading);
            counters.CountStored();
        }

        return new IngestionOutcome(counters.Snapshot(), stored);
    }
}
