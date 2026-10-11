namespace I4Twins.Domain.Rules;

public sealed record RuleResult(string RuleId, string RuleName, bool Violated, string? Reason)
{
    public static RuleResult Violation(string RuleId, string RuleName, string Reason) =>
        new(RuleId, RuleName, true, Reason);

    public static RuleResult NotViolated(string RuleId, string RuleName) =>
        new(RuleId, RuleName, false, null);
}
