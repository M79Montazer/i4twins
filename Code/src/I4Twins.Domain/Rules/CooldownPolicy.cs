namespace I4Twins.Domain.Rules;

public sealed class CooldownPolicy(TimeSpan cooldown)
{
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(5);

    public CooldownPolicy() : this(Default)
    {
    }

    public TimeSpan Cooldown { get; } = cooldown;

    public bool AllowsRaise(RuleEpisode episode, DateTimeOffset? previousAlertEndTs) =>
        previousAlertEndTs is null || episode.StartTs - previousAlertEndTs.Value > Cooldown;
}
