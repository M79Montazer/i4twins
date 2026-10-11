using I4Twins.Application.Rules;

namespace I4Twins.Infrastructure.Rules;

public sealed class FileRuleSource(string path) : IRuleSource
{
    public string Read() => File.ReadAllText(path);
}
