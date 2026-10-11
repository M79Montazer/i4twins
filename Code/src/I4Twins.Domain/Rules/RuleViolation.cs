namespace I4Twins.Domain.Rules;

public sealed record RuleViolation(string RuleId, string RuleName, string Reason);
