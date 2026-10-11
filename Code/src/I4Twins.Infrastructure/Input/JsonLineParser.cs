using System.Text.Json;
using I4Twins.Application.Ingestion;
using I4Twins.Domain.Readings;

namespace I4Twins.Infrastructure.Input;

public sealed class JsonLineParser : ILineParser
{
    public LineParseResult Parse(string line)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            return new LineMalformed($"invalid JSON: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new LineMalformed("record is not a JSON object");
            }

            var root = document.RootElement;
            var raw = new RawReading(
                DeviceId: Field(root, "deviceId"),
                Metric: Field(root, "metric"),
                Timestamp: Field(root, "ts"),
                Value: Field(root, "value"),
                Seq: Field(root, "seq"));
            return new LineParsed(raw);
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
            JsonValueKind.Number when element.TryGetInt64(out var integer) => RawField.Of(integer),
            JsonValueKind.Number => RawField.Of(element.GetDouble()),
            _ => new RawField(RawFieldType.Other),
        };
    }
}
