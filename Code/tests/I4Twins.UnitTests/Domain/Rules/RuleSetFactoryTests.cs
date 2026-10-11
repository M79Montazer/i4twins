using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Rules.Operators;

namespace I4Twins.UnitTests.Domain.Rules;

public class RuleSetFactoryTests
{
    private readonly RuleOperatorRegistry registry = RuleOperatorRegistry.WithDefaultOperators();

    private static RawRule Raw(
        RawField? id = null,
        RawField? name = null,
        RawField? enabled = null,
        RawField? metric = null,
        RawField? deviceId = null,
        RawField? ruleOperator = null,
        RawField? threshold = null,
        RawField? min = null,
        RawField? max = null,
        RawField? durationSeconds = null) => new(
        id ?? RawField.Of("R1"),
        name ?? RawField.Of("test rule"),
        enabled ?? RawField.Of(true),
        metric ?? RawField.Of("temperature"),
        deviceId ?? new RawField(RawFieldType.Missing),
        ruleOperator ?? RawField.Of("GreaterThan"),
        threshold ?? RawField.Of(80),
        min ?? new RawField(RawFieldType.Missing),
        max ?? new RawField(RawFieldType.Missing),
        durationSeconds ?? new RawField(RawFieldType.Missing));

    private static RawField Missing() => new(RawFieldType.Missing);

    [Fact]
    public void ValidRule_AllFieldsAreMapped()
    {
        var result = RuleSetFactory.Create(
        [
            Raw(
                id: RawField.Of("R7"),
                name: RawField.Of("PUMP-01 Pressure Limit"),
                metric: RawField.Of("pressure"),
                deviceId: RawField.Of("PUMP-01"),
                threshold: RawField.Of(150.5)),
        ], registry);

        Assert.True(result.IsValid);
        var rule = Assert.Single(result.RuleSet!.Rules);
        Assert.Equal("R7", rule.Id);
        Assert.Equal("PUMP-01 Pressure Limit", rule.Name);
        Assert.True(rule.Enabled);
        Assert.Equal("pressure", rule.Metric);
        Assert.Equal("PUMP-01", rule.DeviceId);
        Assert.Equal("GreaterThan", rule.OperatorName);
        Assert.Equal(150.5, rule.Threshold);
        Assert.Null(rule.Min);
        Assert.Null(rule.Max);
        Assert.Null(rule.DurationSeconds);
    }

    [Fact]
    public void IntegerAndDecimalParameters_AreBothAccepted()
    {
        var result = RuleSetFactory.Create(
        [
            Raw(id: RawField.Of("R1"), threshold: RawField.Of(80)),
            Raw(id: RawField.Of("R2"), ruleOperator: RawField.Of("Between"), threshold: Missing(), min: RawField.Of(0.5), max: RawField.Of(7)),
        ], registry);

        Assert.True(result.IsValid);
        Assert.Equal(80, result.RuleSet!.Rules[0].Threshold);
        Assert.Equal(0.5, result.RuleSet.Rules[1].Min);
        Assert.Equal(7, result.RuleSet.Rules[1].Max);
    }

    [Fact]
    public void MissingId_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(id: Missing())], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("id must be a non-empty string"));
    }

    [Fact]
    public void EmptyId_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(id: RawField.Of(""))], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("id must be a non-empty string"));
    }

    [Fact]
    public void NullId_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(id: new RawField(RawFieldType.Null))], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("id must be a non-empty string"));
    }

    [Fact]
    public void DuplicateId_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(), Raw()], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate rule id"));
    }

    [Fact]
    public void MissingName_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(name: Missing())], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name must be a non-empty string"));
    }

    [Fact]
    public void MissingMetric_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(metric: Missing())], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("metric must be a non-empty string"));
    }

    [Fact]
    public void MissingEnabled_DefaultsToDisabled()
    {
        var result = RuleSetFactory.Create([Raw(enabled: Missing())], registry);

        Assert.True(result.IsValid);
        Assert.False(result.RuleSet!.Rules[0].Enabled);
    }

    [Fact]
    public void NullEnabled_DefaultsToDisabled()
    {
        var result = RuleSetFactory.Create([Raw(enabled: new RawField(RawFieldType.Null))], registry);

        Assert.True(result.IsValid);
        Assert.False(result.RuleSet!.Rules[0].Enabled);
    }

    [Fact]
    public void ExplicitlyDisabledRule_IsAccepted()
    {
        var result = RuleSetFactory.Create([Raw(enabled: RawField.Of(false))], registry);

        Assert.True(result.IsValid);
        Assert.False(result.RuleSet!.Rules[0].Enabled);
    }

    [Fact]
    public void WrongTypedEnabled_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(enabled: RawField.Of("yes"))], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("enabled must be a boolean"));
    }

    [Fact]
    public void EmptyDeviceId_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(deviceId: RawField.Of(""))], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("deviceId must be a non-empty string when present"));
    }

    [Fact]
    public void NullDeviceId_IsTreatedAsGlobal()
    {
        var result = RuleSetFactory.Create([Raw(deviceId: new RawField(RawFieldType.Null))], registry);

        Assert.True(result.IsValid);
        Assert.Null(result.RuleSet!.Rules[0].DeviceId);
    }

    [Fact]
    public void MissingOperator_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(ruleOperator: Missing())], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("operator must be a non-empty string"));
    }

    [Fact]
    public void UnknownOperator_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(ruleOperator: RawField.Of("NoSuchOperator"))], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknown operator 'NoSuchOperator'"));
    }

    [Fact]
    public void MissingThreshold_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(threshold: Missing())], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("threshold must be a finite number"));
    }

    [Fact]
    public void TextThreshold_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create([Raw(threshold: RawField.Of("80"))], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("threshold must be a finite number"));
    }

    [Fact]
    public void Between_RequiresMinAndMax()
    {
        var missingMin = RuleSetFactory.Create(
            [Raw(ruleOperator: RawField.Of("Between"), threshold: Missing(), min: Missing(), max: RawField.Of(7))], registry);
        var missingMax = RuleSetFactory.Create(
            [Raw(ruleOperator: RawField.Of("Between"), threshold: Missing(), min: RawField.Of(0), max: Missing())], registry);

        Assert.Contains(missingMin.Errors, e => e.Contains("min must be a finite number"));
        Assert.Contains(missingMax.Errors, e => e.Contains("max must be a finite number"));
    }

    [Fact]
    public void Between_MinGreaterThanMax_IsAConfigurationError()
    {
        var result = RuleSetFactory.Create(
            [Raw(ruleOperator: RawField.Of("Between"), threshold: Missing(), min: RawField.Of(7), max: RawField.Of(0))], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("min must not be greater than max"));
    }

    [Fact]
    public void SustainedAbove_RequiresAPositiveDuration()
    {
        var zero = RuleSetFactory.Create(
            [Raw(ruleOperator: RawField.Of("SustainedAbove"), durationSeconds: RawField.Of(0))], registry);
        var negative = RuleSetFactory.Create(
            [Raw(ruleOperator: RawField.Of("SustainedAbove"), durationSeconds: RawField.Of(-5))], registry);
        var missing = RuleSetFactory.Create(
            [Raw(ruleOperator: RawField.Of("SustainedAbove"), durationSeconds: Missing())], registry);

        Assert.Contains(zero.Errors, e => e.Contains("durationSeconds must be greater than 0"));
        Assert.Contains(negative.Errors, e => e.Contains("durationSeconds must be greater than 0"));
        Assert.Contains(missing.Errors, e => e.Contains("durationSeconds must be a finite number"));
    }

    [Fact]
    public void SustainedAbove_WithValidConfiguration_IsAccepted()
    {
        var result = RuleSetFactory.Create(
            [Raw(ruleOperator: RawField.Of("SustainedAbove"), threshold: RawField.Of(69), durationSeconds: RawField.Of(30))], registry);

        Assert.True(result.IsValid);
        var rule = Assert.Single(result.RuleSet!.Rules);
        Assert.Equal(69, rule.Threshold);
        Assert.Equal(30, rule.DurationSeconds);
    }

    [Fact]
    public void Errors_AreCollectedNotFailFirst()
    {
        var result = RuleSetFactory.Create(
            [Raw(name: Missing(), metric: Missing(), ruleOperator: RawField.Of("NoSuchOperator"))], registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
        Assert.Contains(result.Errors, e => e.Contains("metric"));
        Assert.Contains(result.Errors, e => e.Contains("unknown operator"));
        Assert.True(result.Errors.Count >= 3, "every configuration problem should be reported");
    }

    [Fact]
    public void ErrorMessages_IdentifyTheRulePositionAndId()
    {
        var result = RuleSetFactory.Create([Raw(name: Missing())], registry);

        Assert.Contains(result.Errors, e => e.Contains("rules[0] (id 'R1')"));
    }

    [Fact]
    public void EmptyRuleList_IsAValidEmptySet()
    {
        var result = RuleSetFactory.Create([], registry);

        Assert.True(result.IsValid);
        Assert.Empty(result.RuleSet!.Rules);
    }
}
