using I4Twins.Domain.Rules;

namespace I4Twins.Application.Alerts;

public sealed record AlertDecision(RuleEpisode Episode, bool Raised, DateTimeOffset? PreviousAlertEndTs);
