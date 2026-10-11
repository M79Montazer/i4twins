namespace I4Twins.Domain.Rules;

public sealed record RuleEpisode(
    string RuleId,
    string DeviceId,
    string Metric,
    DateTimeOffset StartTs,
    DateTimeOffset EndTs,
    double PeakValue,
    bool Qualifies);
