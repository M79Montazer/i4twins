namespace I4Twins.Application.Rules;

public sealed class RuleConfigurationException(IReadOnlyList<string> errors)
    : Exception($"rule configuration is invalid: {string.Join("; ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
