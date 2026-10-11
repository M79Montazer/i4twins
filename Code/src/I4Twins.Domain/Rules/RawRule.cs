using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public sealed record RawRule(
    RawField Id,
    RawField Name,
    RawField Enabled,
    RawField Metric,
    RawField DeviceId,
    RawField Operator,
    RawField Threshold,
    RawField Min,
    RawField Max,
    RawField DurationSeconds);
