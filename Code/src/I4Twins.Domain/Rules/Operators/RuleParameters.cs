using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules.Operators;

internal static class RuleParameters
{
    public static bool TryGetFiniteNumber(RawField field, out double value)
    {
        value = field.Type switch
        {
            RawFieldType.Number => field.Number,
            RawFieldType.Integer => field.Integer,
            _ => double.NaN,
        };

        return double.IsFinite(value);
    }
}
