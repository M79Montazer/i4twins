using I4Twins.Domain.Rules;

namespace I4Twins.Application.Rules;

public interface IRuleSource
{
    string Read();
}

public interface IRuleParser
{
    RuleDocumentParseResult Parse(string json);
}
