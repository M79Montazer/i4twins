using System.Diagnostics.CodeAnalysis;
using I4Twins.Domain.Rules.Operators;

namespace I4Twins.Domain.Rules;

public sealed class RuleOperatorRegistry
{
    private readonly Dictionary<string, IRuleOperator> _operators = new(StringComparer.Ordinal);

    public static RuleOperatorRegistry WithDefaultOperators()
    {
        var registry = new RuleOperatorRegistry();
        registry.Register(new GreaterThanOperator());
        registry.Register(new GreaterThanOrEqualOperator());
        registry.Register(new LessThanOperator());
        registry.Register(new LessThanOrEqualOperator());
        registry.Register(new EqualOperator());
        registry.Register(new BetweenOperator());
        registry.Register(new SustainedAboveOperator());
        return registry;
    }

    public void Register(IRuleOperator ruleOperator)
    {
        if (!_operators.TryAdd(ruleOperator.Name, ruleOperator))
        {
            throw new InvalidOperationException($"an operator named '{ruleOperator.Name}' is already registered");
        }
    }

    public bool IsKnown(string name) => _operators.ContainsKey(name);

    public IRuleOperator Get(string name) => _operators[name];

    public bool TryGetStateless(string name, [NotNullWhen(true)] out IStatelessRuleOperator? ruleOperator)
    {
        if (_operators.TryGetValue(name, out var registered) && registered is IStatelessRuleOperator stateless)
        {
            ruleOperator = stateless;
            return true;
        }

        ruleOperator = null;
        return false;
    }

    public bool TryGetStateful(string name, [NotNullWhen(true)] out IStatefulRuleOperator? ruleOperator)
    {
        if (_operators.TryGetValue(name, out var registered) && registered is IStatefulRuleOperator stateful)
        {
            ruleOperator = stateful;
            return true;
        }

        ruleOperator = null;
        return false;
    }
}
