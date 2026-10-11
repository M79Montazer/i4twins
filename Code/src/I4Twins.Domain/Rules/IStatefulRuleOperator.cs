using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules;

public interface IStatefulRuleOperator : IRuleOperator
{
    StatefulStreamResult EvaluateStream(string deviceId, string metric, IReadOnlyList<SensorReading> orderedReadings, RuleDefinition rule);
}
