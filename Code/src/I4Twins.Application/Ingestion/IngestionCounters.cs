namespace I4Twins.Application.Ingestion;

public sealed record IngestionCounterSnapshot(
    long TotalLines,
    long ParsedReadings,
    long MalformedLines,
    long ValidationRejected,
    long DuplicatesRemoved,
    long StoredReadings)
{
    public long InvalidRecords => MalformedLines + ValidationRejected;
}


public sealed class IngestionCounters
{
    public long TotalLines { get; private set; }
    public long ParsedReadings { get; private set; }
    public long MalformedLines { get; private set; }
    public long ValidationRejected { get; private set; }
    public long DuplicatesRemoved { get; private set; }
    public long StoredReadings { get; private set; }

    public void CountTotalLine() => TotalLines++;
    public void CountParsed() => ParsedReadings++;
    public void CountMalformed() => MalformedLines++;
    public void CountValidationRejected() => ValidationRejected++;
    public void CountDuplicate() => DuplicatesRemoved++;
    public void CountStored() => StoredReadings++;

    public IngestionCounterSnapshot Snapshot() => new(
        TotalLines, ParsedReadings, MalformedLines, ValidationRejected, DuplicatesRemoved, StoredReadings);
}
