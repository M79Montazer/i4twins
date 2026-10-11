using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public sealed record RuleDefinition(
    string Id,
    string Name,
    bool Enabled,
    string Metric,
    string? DeviceId,
    string OperatorName,
    double? Threshold,
    double? Min,
    double? Max,
    double? DurationSeconds)
{
    public bool AppliesTo(SensorReading reading) =>
        Enabled
        && Metric == reading.Metric
        && (DeviceId is null || DeviceId == reading.DeviceId);
}
