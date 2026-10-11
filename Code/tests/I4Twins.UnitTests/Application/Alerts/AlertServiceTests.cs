using System.Globalization;
using I4Twins.Application.Alerts;
using I4Twins.Domain.Rules;

namespace I4Twins.UnitTests.Application.Alerts;

public class AlertServiceTests
{
    private static DateTimeOffset Ts(string iso) => DateTimeOffset.Parse(iso, styles: DateTimeStyles.AssumeUniversal);

    private static RuleEpisode Episode(
        string ruleId = "R2",
        string deviceId = "PUMP-01",
        string metric = "temperature",
        string start = "2025-06-01T08:00:00Z",
        string end = "2025-06-01T08:01:00Z",
        double peak = 90,
        bool qualifies = true) =>
        new(ruleId, deviceId, metric, Ts(start), Ts(end), peak, qualifies);

    private static AlertService Service(double cooldownMinutes = 5) =>
        new(new CooldownPolicy(TimeSpan.FromMinutes(cooldownMinutes)));

    [Fact]
    public void OneLongEpisode_ProducesExactlyOneAlert()
    {
        var outcome = Service().Evaluate([Episode()]);

        var alert = Assert.Single(outcome.Alerts);
        Assert.Equal("R2", alert.RuleId);
        Assert.Equal(1, outcome.Counters.AlertsRaised);
        Assert.Equal(0, outcome.Counters.AlertsSuppressed);
    }

    [Fact]
    public void NonQualifyingEpisodes_AreNeverConsideredForAlerting()
    {
        var outcome = Service().Evaluate([Episode(qualifies: false), Episode(start: "2025-06-01T09:00:00Z", end: "2025-06-01T09:02:00Z", qualifies: false)]);

        Assert.Empty(outcome.Alerts);
        Assert.Empty(outcome.Decisions);
        Assert.Equal(0, outcome.Counters.QualifyingEpisodes);
    }

    [Fact]
    public void EpisodeWithinCooldown_IsSuppressed_ButStillTracked()
    {
        var first = Episode();
        var second = Episode(start: "2025-06-01T08:04:00Z", end: "2025-06-01T08:06:00Z");

        var outcome = Service().Evaluate([first, second]);

        Assert.Single(outcome.Alerts);
        Assert.Equal(1, outcome.Counters.AlertsRaised);
        Assert.Equal(1, outcome.Counters.AlertsSuppressed);
        var suppressed = Assert.Single(outcome.Decisions, d => !d.Raised);
        Assert.True(suppressed.Episode.Qualifies, "the suppressed episode is still recorded");
        Assert.Equal(Ts("2025-06-01T08:01:00Z"), suppressed.PreviousAlertEndTs);
    }

    [Fact]
    public void EpisodeExactlyAtCooldownBoundary_IsSuppressed_SeparationMustBeStrictlyMore()
    {
        var outcome = Service().Evaluate([
            Episode(),
            Episode(start: "2025-06-01T08:06:00Z", end: "2025-06-01T08:08:00Z"),
        ]);

        Assert.Equal(1, outcome.Counters.AlertsRaised);
        Assert.Equal(1, outcome.Counters.AlertsSuppressed);
    }

    [Fact]
    public void EpisodeAfterCooldown_RaisesANewAlert_AnchoredToThePreviousAlertEnd()
    {
        var outcome = Service().Evaluate([
            Episode(),
            Episode(start: "2025-06-01T08:06:00.0000001Z", end: "2025-06-01T08:08:00Z"),
        ]);

        Assert.Equal(2, outcome.Alerts.Count);
        Assert.Equal(2, outcome.Counters.AlertsRaised);
        Assert.Equal(0, outcome.Counters.AlertsSuppressed);
    }

    [Fact]
    public void SuppressedEpisode_DoesNotMoveTheCooldownAnchor()
    {
        var outcome = Service().Evaluate([
            Episode(),
            Episode(start: "2025-06-01T08:03:00Z", end: "2025-06-01T08:04:00Z"),
            Episode(start: "2025-06-01T08:06:00.0000001Z", end: "2025-06-01T08:08:00Z"),
        ]);

        Assert.Equal(2, outcome.Alerts.Count);
        Assert.Single(outcome.Alerts, a => a.StartTs == Ts("2025-06-01T08:06:00.0000001Z"));
        Assert.Equal(1, outcome.Counters.AlertsSuppressed);
    }

    [Fact]
    public void Cooldown_IsIsolatedBetweenRules()
    {
        var outcome = Service().Evaluate([
            Episode(ruleId: "R1"),
            Episode(ruleId: "R2", start: "2025-06-01T08:02:00Z", end: "2025-06-01T08:03:00Z"),
        ]);

        Assert.Equal(2, outcome.Alerts.Count);
    }

    [Fact]
    public void Cooldown_IsIsolatedBetweenDevices()
    {
        var outcome = Service().Evaluate([
            Episode(deviceId: "PUMP-01"),
            Episode(deviceId: "PUMP-02", start: "2025-06-01T08:02:00Z", end: "2025-06-01T08:03:00Z"),
        ]);

        Assert.Equal(2, outcome.Alerts.Count);
    }

    [Fact]
    public void Cooldown_IsIsolatedBetweenMetrics()
    {
        var outcome = Service().Evaluate([
            Episode(metric: "temperature"),
            Episode(metric: "pressure", start: "2025-06-01T08:02:00Z", end: "2025-06-01T08:03:00Z"),
        ]);

        Assert.Equal(2, outcome.Alerts.Count);
    }

    [Fact]
    public void Alert_CarriesEpisodeTimestampsAndPeak()
    {
        var outcome = Service().Evaluate([Episode(peak: 87.5)]);

        var alert = Assert.Single(outcome.Alerts);
        Assert.Equal("R2", alert.RuleId);
        Assert.Equal("PUMP-01", alert.DeviceId);
        Assert.Equal("temperature", alert.Metric);
        Assert.Equal(Ts("2025-06-01T08:00:00Z"), alert.StartTs);
        Assert.Equal(Ts("2025-06-01T08:01:00Z"), alert.EndTs);
        Assert.Equal(87.5, alert.PeakValue);
    }

    [Fact]
    public void EndOfInputEpisode_AlertsWithItsRecordedTimestamps()
    {
        var outcome = Service().Evaluate([Episode(start: "2025-06-01T08:00:00Z", end: "2025-06-01T08:00:40Z")]);

        var alert = Assert.Single(outcome.Alerts);
        Assert.Equal(Ts("2025-06-01T08:00:40Z"), alert.EndTs);
    }

    [Fact]
    public void Evaluation_IsDeterministic_RegardlessOfEpisodeInputOrder()
    {
        var episodes = new[]
        {
            Episode(),
            Episode(start: "2025-06-01T08:04:00Z", end: "2025-06-01T08:06:00Z"),
            Episode(ruleId: "R1"),
        };

        var first = Service().Evaluate(episodes);
        var second = Service().Evaluate([.. episodes.Reverse()]);

        Assert.Equal(first.Alerts, second.Alerts);
        Assert.Equal(first.Counters, second.Counters);
        Assert.Equal(
            first.Decisions.Select(d => (d.Episode, d.Raised)),
            second.Decisions.Select(d => (d.Episode, d.Raised)));
    }

    [Fact]
    public void CustomCooldown_IsHonored()
    {
        var outcome = Service(cooldownMinutes: 1).Evaluate([
            Episode(),
            Episode(start: "2025-06-01T08:02:00.0000001Z", end: "2025-06-01T08:03:00Z"),
        ]);

        Assert.Equal(2, outcome.Alerts.Count);
    }
}
