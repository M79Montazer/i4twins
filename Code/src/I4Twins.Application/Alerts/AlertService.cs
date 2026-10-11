using I4Twins.Domain.Rules;

namespace I4Twins.Application.Alerts;

public sealed record AlertOutcome(
    IReadOnlyList<Alert> Alerts,
    IReadOnlyList<AlertDecision> Decisions,
    AlertCounterSnapshot Counters);

public sealed class AlertService(CooldownPolicy policy)
{
    public AlertService() : this(new CooldownPolicy())
    {
    }

    public AlertOutcome Evaluate(IReadOnlyList<RuleEpisode> episodes)
    {
        var candidates = episodes
            .Where(episode => episode.Qualifies)
            .OrderBy(episode => episode.RuleId, StringComparer.Ordinal)
            .ThenBy(episode => episode.DeviceId, StringComparer.Ordinal)
            .ThenBy(episode => episode.Metric, StringComparer.Ordinal)
            .ThenBy(episode => episode.StartTs)
            .ToList();

        var counters = new AlertCounters();
        var decisions = new List<AlertDecision>(candidates.Count);
        var alerts = new List<Alert>();
        var lastAlertEndByStream = new Dictionary<(string RuleId, string DeviceId, string Metric), DateTimeOffset>();

        foreach (var episode in candidates)
        {
            var key = (episode.RuleId, episode.DeviceId, episode.Metric);
            var previousAlertEnd = lastAlertEndByStream.TryGetValue(key, out var end) ? end : (DateTimeOffset?)null;
            var raised = policy.AllowsRaise(episode, previousAlertEnd);
            counters.CountCandidate();

            if (raised)
            {
                alerts.Add(new Alert(
                    episode.RuleId, episode.DeviceId, episode.Metric, episode.StartTs, episode.EndTs, episode.PeakValue));
                lastAlertEndByStream[key] = episode.EndTs;
                counters.CountRaised();
            }
            else
            {
                counters.CountSuppressed();
            }

            decisions.Add(new AlertDecision(episode, raised, previousAlertEnd));
        }

        return new AlertOutcome(alerts, decisions, counters.Snapshot());
    }
}
