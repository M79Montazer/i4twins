using I4Twins.Domain.Readings;

namespace I4Twins.Application.Ingestion;

public sealed record IngestionOutcome(IngestionCounterSnapshot Counters, IReadOnlyList<SensorReading> Readings);


public sealed class IngestionService(ILineSource source, ILineParser parser)
{
    public IngestionOutcome Run()
    {
        var counters = new IngestionCounters();
        var duplicates = new DuplicateFilter();
        var stored = new List<SensorReading>();

        foreach (var line in source.ReadLines())
        {
            counters.CountTotalLine();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parseResult = parser.Parse(line);
            if (parseResult is LineMalformed malformed)
            {
                counters.CountMalformed();
                continue;
            }

            counters.CountParsed();
            var raw = ((LineParsed)parseResult).Reading;

            var validation = ReadingValidator.Validate(raw);
            if (!validation.IsValid)
            {
                counters.CountValidationRejected();
                continue;
            }

            var reading = validation.Reading!;
            if (duplicates.IsDuplicate(reading))
            {
                counters.CountDuplicate();
                continue;
            }

            stored.Add(reading);
            counters.CountStored();
        }

        return new IngestionOutcome(counters.Snapshot(), stored);
    }
}
