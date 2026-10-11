namespace I4Twins.Domain.Rules;

public abstract record RuleDocumentParseResult;

public sealed record RuleDocumentParsed(IReadOnlyList<RawRule> Rules) : RuleDocumentParseResult;

public sealed record RuleDocumentMalformed(string Reason) : RuleDocumentParseResult;
