namespace I4Twins.Application.Alerts;

public sealed record AlertCounterSnapshot(long QualifyingEpisodes, long AlertsRaised, long AlertsSuppressed);

public sealed class AlertCounters
{
    public long QualifyingEpisodes { get; private set; }
    public long AlertsRaised { get; private set; }
    public long AlertsSuppressed { get; private set; }

    public void CountCandidate() => QualifyingEpisodes++;
    public void CountRaised() => AlertsRaised++;
    public void CountSuppressed() => AlertsSuppressed++;

    public AlertCounterSnapshot Snapshot() => new(QualifyingEpisodes, AlertsRaised, AlertsSuppressed);
}
