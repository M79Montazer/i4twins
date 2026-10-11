using System.Globalization;
using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;

namespace I4Twins.UnitTests.Domain.Rules;

public class RuleApplicabilityTests
{
    private static RuleDefinition Rule(bool enabled = true, string metric = "temperature", string? deviceId = null) =>
        new("R1", "test rule", enabled, metric, deviceId, "GreaterThan", 80, null, null, null);

    private static SensorReading Reading(string deviceId = "PUMP-01", string metric = "temperature") =>
        new(deviceId, metric, DateTimeOffset.Parse("2025-06-01T08:00:00Z", styles: DateTimeStyles.AssumeUniversal), 90, 1);

    [Fact]
    public void GlobalRule_AppliesToEveryDeviceWithTheMetric()
    {
        var rule = Rule(deviceId: null);

        Assert.True(rule.AppliesTo(Reading(deviceId: "PUMP-01")));
        Assert.True(rule.AppliesTo(Reading(deviceId: "PUMP-02")));
    }

    [Fact]
    public void DeviceRule_AppliesOnlyToTheTargetedDevice()
    {
        var rule = Rule(deviceId: "PUMP-01");

        Assert.True(rule.AppliesTo(Reading(deviceId: "PUMP-01")));
        Assert.False(rule.AppliesTo(Reading(deviceId: "PUMP-02")));
    }

    [Fact]
    public void Rule_DoesNotApplyToOtherMetrics()
    {
        var rule = Rule(metric: "temperature");

        Assert.False(rule.AppliesTo(Reading(metric: "pressure")));
    }

    [Fact]
    public void DisabledRule_DoesNotApply()
    {
        var rule = Rule(enabled: false);

        Assert.False(rule.AppliesTo(Reading()));
    }

    [Fact]
    public void DeviceRule_RequiresBothDeviceAndMetricToMatch()
    {
        var rule = Rule(metric: "temperature", deviceId: "PUMP-01");

        Assert.False(rule.AppliesTo(Reading(deviceId: "PUMP-01", metric: "pressure")));
        Assert.False(rule.AppliesTo(Reading(deviceId: "PUMP-02", metric: "temperature")));
        Assert.True(rule.AppliesTo(Reading(deviceId: "PUMP-01", metric: "temperature")));
    }
}
