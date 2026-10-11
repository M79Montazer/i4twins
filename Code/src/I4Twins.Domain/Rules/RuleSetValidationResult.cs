namespace I4Twins.Domain.Rules;

public sealed record RuleSetValidationResult(RuleSet? RuleSet, IReadOnlyList<string> Errors)
{
    public bool IsValid => RuleSet is not null;

    public static RuleSetValidationResult Ok(RuleSet ruleSet) => new(ruleSet, []);
    public static RuleSetValidationResult Invalid(IReadOnlyList<string> errors) => new(null, errors);
}
