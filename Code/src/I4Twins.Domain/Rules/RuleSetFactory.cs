using I4Twins.Domain.Readings;
using I4Twins.Domain.Rules.Operators;

namespace I4Twins.Domain.Rules;

public static class RuleSetFactory
{
    public static RuleSetValidationResult Create(IReadOnlyList<RawRule> rawRules, RuleOperatorRegistry registry)
    {
        var errors = new List<string>();
        var rules = new List<RuleDefinition>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < rawRules.Count; index++)
        {
            var raw = rawRules[index];
            var errorsBefore = errors.Count;
            var context = $"rules[{index}]";

            string id = "";
            if (!TryGetNonEmptyText(raw.Id, out var idValue))
            {
                errors.Add($"{context}: id must be a non-empty string");
            }
            else
            {
                id = idValue;
                context = $"{context} (id '{id}')";
                if (!seenIds.Add(id))
                {
                    errors.Add($"{context}: duplicate rule id");
                }
            }

            if (!TryGetNonEmptyText(raw.Name, out var name))
            {
                errors.Add($"{context}: name must be a non-empty string");
            }

            if (!TryGetNonEmptyText(raw.Metric, out var metric))
            {
                errors.Add($"{context}: metric must be a non-empty string");
            }

            string? deviceId = null;
            if (raw.DeviceId.Type is not (RawFieldType.Missing or RawFieldType.Null))
            {
                if (!TryGetNonEmptyText(raw.DeviceId, out var deviceValue))
                {
                    errors.Add($"{context}: deviceId must be a non-empty string when present");
                }
                else
                {
                    deviceId = deviceValue;
                }
            }

            var enabled = false;
            switch (raw.Enabled.Type)
            {
                case RawFieldType.Missing or RawFieldType.Null:
                    break;
                case RawFieldType.Boolean:
                    enabled = raw.Enabled.Boolean;
                    break;
                default:
                    errors.Add($"{context}: enabled must be a boolean when present");
                    break;
            }

            string operatorName = "";
            IRuleOperator? ruleOperator = null;
            if (!TryGetNonEmptyText(raw.Operator, out var operatorValue))
            {
                errors.Add($"{context}: operator must be a non-empty string");
            }
            else if (!registry.IsKnown(operatorValue))
            {
                errors.Add($"{context}: unknown operator '{operatorValue}'");
            }
            else
            {
                operatorName = operatorValue;
                ruleOperator = registry.Get(operatorName);
            }

            if (ruleOperator is not null)
            {
                errors.AddRange(ruleOperator.ValidateConfiguration(raw).Select(message => $"{context}: {message}"));
            }

            if (errors.Count == errorsBefore)
            {
                rules.Add(new RuleDefinition(
                    id,
                    name,
                    enabled,
                    metric,
                    deviceId,
                    operatorName,
                    NumberOrNull(raw.Threshold),
                    NumberOrNull(raw.Min),
                    NumberOrNull(raw.Max),
                    NumberOrNull(raw.DurationSeconds)));
            }
        }

        return errors.Count == 0
            ? RuleSetValidationResult.Ok(new RuleSet(rules))
            : RuleSetValidationResult.Invalid(errors);
    }

    private static double? NumberOrNull(RawField field) =>
        RuleParameters.TryGetFiniteNumber(field, out var value) ? value : null;

    private static bool TryGetNonEmptyText(RawField field, out string value)
    {
        value = field.Text ?? string.Empty;
        return field.Type == RawFieldType.Text && !string.IsNullOrWhiteSpace(value);
    }
}
