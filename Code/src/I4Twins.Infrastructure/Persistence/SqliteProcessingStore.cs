using I4Twins.Application.Persistence;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Readings;
using Microsoft.Data.Sqlite;

namespace I4Twins.Infrastructure.Persistence;

public sealed class SqliteProcessingStore(string databasePath) : IProcessingStore
{
    private const string CreateSchema = """
        CREATE TABLE IF NOT EXISTS readings (
            device_id   TEXT    NOT NULL,
            metric      TEXT    NOT NULL,
            ts          TEXT    NOT NULL,
            seq         INTEGER NOT NULL,
            value       REAL    NOT NULL,
            acceptable  INTEGER NOT NULL,
            PRIMARY KEY (device_id, metric, ts, seq)
        );

        CREATE TABLE IF NOT EXISTS rule_results (
            device_id   TEXT    NOT NULL,
            metric      TEXT    NOT NULL,
            ts          TEXT    NOT NULL,
            seq         INTEGER NOT NULL,
            rule_id     TEXT    NOT NULL,
            rule_name   TEXT    NOT NULL,
            violated    INTEGER NOT NULL,
            reason      TEXT,
            PRIMARY KEY (device_id, metric, ts, seq, rule_id)
        );

        CREATE TABLE IF NOT EXISTS episodes (
            rule_id      TEXT    NOT NULL,
            device_id    TEXT    NOT NULL,
            metric       TEXT    NOT NULL,
            start_ts     TEXT    NOT NULL,
            end_ts       TEXT    NOT NULL,
            peak_value   REAL    NOT NULL,
            qualifies    INTEGER NOT NULL,
            alert_raised INTEGER NOT NULL,
            PRIMARY KEY (rule_id, device_id, metric, start_ts)
        );

        CREATE TABLE IF NOT EXISTS alerts (
            rule_id    TEXT NOT NULL,
            device_id  TEXT NOT NULL,
            metric     TEXT NOT NULL,
            start_ts   TEXT NOT NULL,
            end_ts     TEXT NOT NULL,
            peak_value REAL NOT NULL,
            PRIMARY KEY (rule_id, device_id, metric, start_ts)
        );
        """;

    public PersistenceResult Persist(ProcessingBatch batch)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var newReadings = InsertReadings(connection, transaction, batch.Evaluations);
            var newRuleResults = InsertRuleResults(connection, transaction, batch.Evaluations);
            var newEpisodes = InsertEpisodes(connection, transaction, batch.Episodes, batch.RaisedAlerts);
            var newAlerts = InsertAlerts(connection, transaction, batch.RaisedAlerts);
            transaction.Commit();
            return new PersistenceResult(newReadings, newRuleResults, newEpisodes, newAlerts);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = CreateSchema;
        command.ExecuteNonQuery();
        return connection;
    }

    private static long InsertReadings(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<ReadingEvaluation> evaluations)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO readings (device_id, metric, ts, seq, value, acceptable)
            VALUES ($device_id, $metric, $ts, $seq, $value, $acceptable)
            """;
        var deviceId = command.Parameters.Add("$device_id", SqliteType.Text);
        var metric = command.Parameters.Add("$metric", SqliteType.Text);
        var ts = command.Parameters.Add("$ts", SqliteType.Text);
        var seq = command.Parameters.Add("$seq", SqliteType.Integer);
        var value = command.Parameters.Add("$value", SqliteType.Real);
        var acceptable = command.Parameters.Add("$acceptable", SqliteType.Integer);

        var inserted = 0L;
        foreach (var evaluation in evaluations)
        {
            var reading = evaluation.Reading;
            deviceId.Value = reading.DeviceId;
            metric.Value = reading.Metric;
            ts.Value = Timestamp.Encode(reading.Timestamp);
            seq.Value = reading.Seq;
            value.Value = reading.Value;
            acceptable.Value = evaluation.Acceptable ? 1 : 0;
            inserted += command.ExecuteNonQuery();
        }

        return inserted;
    }

    private static long InsertRuleResults(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<ReadingEvaluation> evaluations)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO rule_results (device_id, metric, ts, seq, rule_id, rule_name, violated, reason)
            VALUES ($device_id, $metric, $ts, $seq, $rule_id, $rule_name, $violated, $reason)
            """;
        var deviceId = command.Parameters.Add("$device_id", SqliteType.Text);
        var metric = command.Parameters.Add("$metric", SqliteType.Text);
        var ts = command.Parameters.Add("$ts", SqliteType.Text);
        var seq = command.Parameters.Add("$seq", SqliteType.Integer);
        var ruleId = command.Parameters.Add("$rule_id", SqliteType.Text);
        var ruleName = command.Parameters.Add("$rule_name", SqliteType.Text);
        var violated = command.Parameters.Add("$violated", SqliteType.Integer);
        var reason = command.Parameters.Add("$reason", SqliteType.Text);

        var inserted = 0L;
        foreach (var evaluation in evaluations)
        {
            var reading = evaluation.Reading;
            deviceId.Value = reading.DeviceId;
            metric.Value = reading.Metric;
            ts.Value = Timestamp.Encode(reading.Timestamp);
            seq.Value = reading.Seq;
            foreach (var result in evaluation.RuleResults)
            {
                ruleId.Value = result.RuleId;
                ruleName.Value = result.RuleName;
                violated.Value = result.Violated ? 1 : 0;
                reason.Value = (object?)result.Reason ?? DBNull.Value;
                inserted += command.ExecuteNonQuery();
            }
        }

        return inserted;
    }

    private static long InsertEpisodes(
        SqliteConnection connection, SqliteTransaction transaction,
        IReadOnlyList<RuleEpisode> episodes, IReadOnlyList<Alert> raisedAlerts)
    {
        var raisedKeys = raisedAlerts
            .Select(alert => (alert.RuleId, alert.DeviceId, alert.Metric, alert.StartTs))
            .ToHashSet();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO episodes (rule_id, device_id, metric, start_ts, end_ts, peak_value, qualifies, alert_raised)
            VALUES ($rule_id, $device_id, $metric, $start_ts, $end_ts, $peak_value, $qualifies, $alert_raised)
            """;
        var ruleId = command.Parameters.Add("$rule_id", SqliteType.Text);
        var deviceId = command.Parameters.Add("$device_id", SqliteType.Text);
        var metric = command.Parameters.Add("$metric", SqliteType.Text);
        var startTs = command.Parameters.Add("$start_ts", SqliteType.Text);
        var endTs = command.Parameters.Add("$end_ts", SqliteType.Text);
        var peakValue = command.Parameters.Add("$peak_value", SqliteType.Real);
        var qualifies = command.Parameters.Add("$qualifies", SqliteType.Integer);
        var alertRaised = command.Parameters.Add("$alert_raised", SqliteType.Integer);

        var inserted = 0L;
        foreach (var episode in episodes)
        {
            ruleId.Value = episode.RuleId;
            deviceId.Value = episode.DeviceId;
            metric.Value = episode.Metric;
            startTs.Value = Timestamp.Encode(episode.StartTs);
            endTs.Value = Timestamp.Encode(episode.EndTs);
            peakValue.Value = episode.PeakValue;
            qualifies.Value = episode.Qualifies ? 1 : 0;
            alertRaised.Value = raisedKeys.Contains((episode.RuleId, episode.DeviceId, episode.Metric, episode.StartTs)) ? 1 : 0;
            inserted += command.ExecuteNonQuery();
        }

        return inserted;
    }

    private static long InsertAlerts(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Alert> alerts)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO alerts (rule_id, device_id, metric, start_ts, end_ts, peak_value)
            VALUES ($rule_id, $device_id, $metric, $start_ts, $end_ts, $peak_value)
            """;
        var ruleId = command.Parameters.Add("$rule_id", SqliteType.Text);
        var deviceId = command.Parameters.Add("$device_id", SqliteType.Text);
        var metric = command.Parameters.Add("$metric", SqliteType.Text);
        var startTs = command.Parameters.Add("$start_ts", SqliteType.Text);
        var endTs = command.Parameters.Add("$end_ts", SqliteType.Text);
        var peakValue = command.Parameters.Add("$peak_value", SqliteType.Real);

        var inserted = 0L;
        foreach (var alert in alerts)
        {
            ruleId.Value = alert.RuleId;
            deviceId.Value = alert.DeviceId;
            metric.Value = alert.Metric;
            startTs.Value = Timestamp.Encode(alert.StartTs);
            endTs.Value = Timestamp.Encode(alert.EndTs);
            peakValue.Value = alert.PeakValue;
            inserted += command.ExecuteNonQuery();
        }

        return inserted;
    }
}
