namespace I4Twins.Domain.Rules;

public sealed record Alert(
    string RuleId,
    string DeviceId,
    string Metric,
    DateTimeOffset StartTs,
    DateTimeOffset EndTs,
    double PeakValue);
