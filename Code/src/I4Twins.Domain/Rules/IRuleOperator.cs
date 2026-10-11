using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public interface IRuleOperator
{
    string Name { get; }

    IEnumerable<string> ValidateConfiguration(RawRule rule);
}

public interface IStatelessRuleOperator : IRuleOperator
{
    string? EvaluateViolation(SensorReading reading, RuleDefinition rule);
}
