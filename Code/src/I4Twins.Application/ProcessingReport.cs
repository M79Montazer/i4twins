namespace I4Twins.Application;

public static class ProcessingReport
{
    public static string Format(ProcessingOutcome outcome) => $"""

        Processing report
        =================
        Total lines read:           {outcome.Ingestion.TotalLines}
        Parsed readings:            {outcome.Ingestion.ParsedReadings}
        Stored readings:            {outcome.Ingestion.StoredReadings}
        Duplicates removed:         {outcome.Ingestion.DuplicatesRemoved}
        Invalid records rejected:   {outcome.Ingestion.InvalidRecords} (malformed: {outcome.Ingestion.MalformedLines}, invalid fields: {outcome.Ingestion.ValidationRejected})
        Rules loaded:               {outcome.RulesLoaded}
        Rule evaluations performed: {outcome.Evaluation.EvaluationsPerformed}
        Acceptable readings:        {outcome.Evaluation.AcceptableReadings}
        Unacceptable readings:      {outcome.Evaluation.UnacceptableReadings}
        Rule violations:            {outcome.Evaluation.RuleViolations}
        Episodes detected:          {outcome.Evaluation.EpisodesDetected} (qualifying: {outcome.Evaluation.QualifyingEpisodes})
        Alerts generated:           {outcome.Alerts.AlertsRaised} (suppressed by cooldown: {outcome.Alerts.AlertsSuppressed})
        Rows inserted this run:     {outcome.Persistence.NewRows} (readings: {outcome.Persistence.NewReadings}, rule results: {outcome.Persistence.NewRuleResults}, episodes: {outcome.Persistence.NewEpisodes}, alerts: {outcome.Persistence.NewAlerts})
        """;
}
