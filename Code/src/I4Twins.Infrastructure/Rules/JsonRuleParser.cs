using System.Text.Json;
using I4Twins.Application.Rules;
using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules;

namespace I4Twins.Infrastructure.Rules;

public sealed class JsonRuleParser : IRuleParser
{
    public RuleDocumentParseResult Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new RuleDocumentMalformed($"invalid JSON: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new RuleDocumentMalformed("rules document must be a JSON array");
            }

            var rules = new List<RawRule>();
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    return new RuleDocumentMalformed($"rules[{index}] is not a JSON object");
                }

                rules.Add(new RawRule(
                    Id: Field(element, "id"),
                    Name: Field(element, "name"),
                    Enabled: Field(element, "enabled"),
                    Metric: Field(element, "metric"),
                    DeviceId: Field(element, "deviceId"),
                    Operator: Field(element, "operator"),
                    Threshold: Field(element, "threshold"),
                    Min: Field(element, "min"),
                    Max: Field(element, "max"),
                    DurationSeconds: Field(element, "durationSeconds")));
                index++;
            }

            return new RuleDocumentParsed(rules);
        }
    }

    private static RawField Field(JsonElement record, string name)
    {
        if (!record.TryGetProperty(name, out var element))
        {
            return new RawField(RawFieldType.Missing);
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null => new RawField(RawFieldType.Null),
            JsonValueKind.String => RawField.Of(element.GetString()!),
            JsonValueKind.True => RawField.Of(true),
            JsonValueKind.False => RawField.Of(false),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => RawField.Of(integer),
            JsonValueKind.Number => RawField.Of(element.GetDouble()),
            _ => new RawField(RawFieldType.Other),
        };
    }
}
