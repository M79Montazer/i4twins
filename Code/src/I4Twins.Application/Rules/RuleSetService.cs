using I4Twins.Domain.Rules;

namespace I4Twins.Application.Rules;

public sealed record RuleSetLoadOutcome(RuleSet RuleSet, long RulesLoaded);


public sealed class RuleSetService(IRuleSource source, IRuleParser parser, RuleOperatorRegistry operators)
{
    public RuleSetLoadOutcome Load()
    {
        var document = parser.Parse(source.Read());
        if (document is RuleDocumentMalformed malformed)
        {
            throw new RuleConfigurationException([malformed.Reason]);
        }

        var result = RuleSetFactory.Create(((RuleDocumentParsed)document).Rules, operators);
        if (!result.IsValid)
        {
            throw new RuleConfigurationException(result.Errors);
        }

        var ruleSet = result.RuleSet!;
        return new RuleSetLoadOutcome(ruleSet, ruleSet.Rules.Count);
    }
}
