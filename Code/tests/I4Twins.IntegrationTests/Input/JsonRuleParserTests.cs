using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;
using I4Twins.Infrastructure.Rules;

namespace I4Twins.IntegrationTests.Input;

public class JsonRuleParserTests
{
    private readonly JsonRuleParser parser = new();

    [Fact]
    public void ValidRules_AreParsedWithTypedFields()
    {
        const string json = """
            [
              {
                "id": "R1",
                "name": "High Temperature",
                "metric": "temperature",
                "operator": "GreaterThan",
                "threshold": 72,
                "enabled": true
              },
              {
                "id": "R2",
                "name": "Sustained",
                "deviceId": "PUMP-01",
                "metric": "temperature",
                "operator": "SustainedAbove",
                "threshold": 69.5,
                "durationSeconds": 30,
                "enabled": false
              }
            ]
            """;

        var result = parser.Parse(json);

        var parsed = Assert.IsType<RuleDocumentParsed>(result);
        Assert.Equal(2, parsed.Rules.Count);

        var first = parsed.Rules[0];
        Assert.Equal(RawFieldType.Text, first.Id.Type);
        Assert.Equal("R1", first.Id.Text);
        Assert.Equal(RawFieldType.Boolean, first.Enabled.Type);
        Assert.True(first.Enabled.Boolean);
        Assert.Equal(RawFieldType.Integer, first.Threshold.Type);
        Assert.Equal(72, first.Threshold.Integer);
        Assert.Equal(RawFieldType.Missing, first.DeviceId.Type);
        Assert.Equal(RawFieldType.Missing, first.DurationSeconds.Type);

        var second = parsed.Rules[1];
        Assert.Equal(RawFieldType.Text, second.DeviceId.Type);
        Assert.Equal("PUMP-01", second.DeviceId.Text);
        Assert.Equal(RawFieldType.Number, second.Threshold.Type);
        Assert.Equal(69.5, second.Threshold.Number);
        Assert.Equal(RawFieldType.Integer, second.DurationSeconds.Type);
        Assert.Equal(30, second.DurationSeconds.Integer);
        Assert.Equal(RawFieldType.Boolean, second.Enabled.Type);
        Assert.False(second.Enabled.Boolean);
    }

    [Fact]
    public void MalformedJson_IsRejectedAsMalformed()
    {
        var result = parser.Parse("{not json");

        var malformed = Assert.IsType<RuleDocumentMalformed>(result);
        Assert.Contains("invalid JSON", malformed.Reason);
    }

    [Fact]
    public void NonArrayRoot_IsRejectedAsMalformed()
    {
        var malformed = Assert.IsType<RuleDocumentMalformed>(parser.Parse("""{"id": "R1"}"""));

        Assert.Contains("JSON array", malformed.Reason);
    }

    [Fact]
    public void NonObjectEntry_IsRejectedAsMalformed()
    {
        var malformed = Assert.IsType<RuleDocumentMalformed>(parser.Parse("""[{"id": "R1"}, 42]"""));

        Assert.Contains("rules[1]", malformed.Reason);
    }

    [Fact]
    public void NullFields_AreDistinguishedFromMissingFields()
    {
        var parsed = Assert.IsType<RuleDocumentParsed>(parser.Parse("""[{"id": null}]"""));

        var rule = parsed.Rules[0];
        Assert.Equal(RawFieldType.Null, rule.Id.Type);
        Assert.Equal(RawFieldType.Missing, rule.Name.Type);
        Assert.Equal(RawFieldType.Missing, rule.Enabled.Type);
    }
}
