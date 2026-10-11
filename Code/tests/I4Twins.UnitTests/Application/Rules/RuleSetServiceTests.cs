using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Readings;

namespace I4Twins.UnitTests.Application.Rules;

public class RuleSetServiceTests
{
    private sealed class StubSource(string content) : IRuleSource
    {
        public string Read() => content;
    }

    private sealed class StubParser(RuleDocumentParseResult result) : IRuleParser
    {
        public RuleDocumentParseResult Parse(string json) => result;
    }

    private static RuleDocumentParseResult ParsedRules(params RawRule[] rules) =>
        new RuleDocumentParsed(rules);

    private static RawRule Raw(string id, string ruleOperator = "GreaterThan") => new(
        RawField.Of(id),
        RawField.Of($"rule {id}"),
        RawField.Of(true),
        RawField.Of("temperature"),
        new RawField(RawFieldType.Missing),
        RawField.Of(ruleOperator),
        RawField.Of(80),
        new RawField(RawFieldType.Missing),
        new RawField(RawFieldType.Missing),
        new RawField(RawFieldType.Missing));

    [Fact]
    public void Load_ValidRules_ReturnsRuleSetAndLoadedCount()
    {
        var service = new RuleSetService(
            new StubSource("unused"),
            new StubParser(ParsedRules(Raw("R1"), Raw("R2"))),
            RuleOperatorRegistry.WithDefaultOperators());

        var outcome = service.Load();

        Assert.Equal(2, outcome.RulesLoaded);
        Assert.Equal(2, outcome.RuleSet.Rules.Count);
    }

    [Fact]
    public void Load_MalformedDocument_ThrowsClearly()
    {
        var service = new RuleSetService(
            new StubSource("unused"),
            new StubParser(new RuleDocumentMalformed("invalid JSON: something")),
            RuleOperatorRegistry.WithDefaultOperators());

        var exception = Assert.Throws<RuleConfigurationException>(service.Load);
        Assert.Contains("invalid JSON: something", exception.Message);
    }

    [Fact]
    public void Load_InvalidRules_ThrowsWithEveryError()
    {
        var invalid = new RawRule(
            Id: new RawField(RawFieldType.Missing),
            Name: new RawField(RawFieldType.Missing),
            Enabled: RawField.Of(true),
            Metric: new RawField(RawFieldType.Missing),
            DeviceId: new RawField(RawFieldType.Missing),
            Operator: RawField.Of("NoSuchOperator"),
            Threshold: new RawField(RawFieldType.Missing),
            Min: new RawField(RawFieldType.Missing),
            Max: new RawField(RawFieldType.Missing),
            DurationSeconds: new RawField(RawFieldType.Missing));

        var service = new RuleSetService(
            new StubSource("unused"),
            new StubParser(ParsedRules(invalid)),
            RuleOperatorRegistry.WithDefaultOperators());

        var exception = Assert.Throws<RuleConfigurationException>(service.Load);
        Assert.True(exception.Errors.Count >= 3, "every configuration problem should be reported");
    }
}
