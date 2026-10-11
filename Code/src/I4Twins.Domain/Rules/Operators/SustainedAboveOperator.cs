using I4Twins.Domain.Readings;

namespace I4Twins.Domain.Rules.Operators;

public sealed class SustainedAboveOperator : IStatefulRuleOperator
{
    public string Name => "SustainedAbove";

    public IEnumerable<string> ValidateConfiguration(RawRule rule)
    {
        if (!RuleParameters.TryGetFiniteNumber(rule.Threshold, out _))
        {
            yield return "threshold must be a finite number";
        }

        if (!RuleParameters.TryGetFiniteNumber(rule.DurationSeconds, out var duration))
        {
            yield return "durationSeconds must be a finite number";
        }
        else if (duration <= 0)
        {
            yield return "durationSeconds must be greater than 0";
        }
    }

    public StatefulStreamResult EvaluateStream(
        string deviceId, string metric, IReadOnlyList<SensorReading> orderedReadings, RuleDefinition rule)
    {
        var threshold = rule.Threshold!.Value;
        var duration = TimeSpan.FromSeconds(rule.DurationSeconds!.Value);
        var episodes = new List<RuleEpisode>();
        var violations = new Dictionary<ReadingKey, RuleViolation>();

        var index = 0;
        while (index < orderedReadings.Count)
        {
            if (orderedReadings[index].Value <= threshold)
            {
                index++;
                continue;
            }

            var start = index;
            while (index < orderedReadings.Count && orderedReadings[index].Value > threshold)
            {
                index++;
            }

            var lastAbove = index - 1;
            var endTs = index < orderedReadings.Count
                ? orderedReadings[index].Timestamp
                : orderedReadings[lastAbove].Timestamp;
            var startTs = orderedReadings[start].Timestamp;
            var qualifies = endTs - startTs >= duration;
            episodes.Add(new RuleEpisode(
                rule.Id,
                deviceId,
                metric,
                startTs,
                endTs,
                PeakValue(orderedReadings, start, lastAbove),
                qualifies));

            if (qualifies)
            {
                var lasted = (endTs - startTs).TotalSeconds;
                for (var k = start; k <= lastAbove; k++)
                {
                    var reading = orderedReadings[k];
                    violations[reading.Key] = new RuleViolation(
                        rule.Id,
                        rule.Name,
                        $"{metric} {NumberText.Of(reading.Value)} remained above {NumberText.Of(threshold)} " +
                        $"for {NumberText.Of(lasted)} s (at least {NumberText.Of(duration.TotalSeconds)} s required)");
                }
            }
        }

        return new StatefulStreamResult(episodes, violations);
    }

    private static double PeakValue(IReadOnlyList<SensorReading> readings, int first, int last)
    {
        var peak = readings[first].Value;
        for (var i = first + 1; i <= last; i++)
        {
            if (readings[i].Value > peak)
            {
                peak = readings[i].Value;
            }
        }

        return peak;
    }
}
