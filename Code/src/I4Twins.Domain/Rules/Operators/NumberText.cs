using System.Globalization;

namespace I4Twins.Domain.Rules.Operators;

internal static class NumberText
{
    public static string Of(double value) => value.ToString(CultureInfo.InvariantCulture);
}
