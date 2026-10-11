using System.Globalization;
using I4Twins.Application.Persistence;
using I4Twins.Domain.Rules;
using I4Twins.Domain.Readings;
using I4Twins.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace I4Twins.IntegrationTests.Persistence;

public class SqliteProcessingStoreTests
{
    private static SensorReading Reading(double value, string ts, long seq, string deviceId = "PUMP-01", string metric = "temperature") =>
        new(deviceId, metric, DateTimeOffset.Parse(ts, styles: DateTimeStyles.AssumeUniversal), value, seq);

    private static ReadingEvaluation Evaluation(SensorReading reading, bool acceptable, params RuleResult[] results) =>
        new(reading, acceptable, results.Length, results);

    private static RuleEpisode Episode(string ruleId = "R2", string start = "2025-06-01T08:00:00Z", string end = "2025-06-01T08:01:00Z", bool qualifies = true) =>
        new(ruleId, "PUMP-01", "temperature",
            DateTimeOffset.Parse(start, styles: DateTimeStyles.AssumeUniversal),
            DateTimeOffset.Parse(end, styles: DateTimeStyles.AssumeUniversal),
            90, qualifies);

    private static Alert Alert(string ruleId = "R2", string start = "2025-06-01T08:00:00Z") =>
        new(ruleId, "PUMP-01", "temperature",
            DateTimeOffset.Parse(start, styles: DateTimeStyles.AssumeUniversal),
            DateTimeOffset.Parse("2025-06-01T08:01:00Z", styles: DateTimeStyles.AssumeUniversal),
            90);

    private static ProcessingBatch Batch() => new(
        [
            Evaluation(Reading(90, "2025-06-01T08:00:00Z", 1), acceptable: false,
                RuleResult.NotViolated("R1", "high temperature"),
                RuleResult.Violation("R2", "sustained", "temperature 90 remained above 80 for 60 s")),
            Evaluation(Reading(70, "2025-06-01T08:01:00Z", 2), acceptable: true,
                RuleResult.NotViolated("R1", "high temperature"),
                RuleResult.NotViolated("R2", "sustained")),
        ],
        [Episode(), Episode(ruleId: "R9", start: "2025-06-01T09:00:00Z", end: "2025-06-01T09:00:10Z", qualifies: false)],
        [Alert()]);

    private static long CountRows(string databasePath, string table)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void Persist_InsertsAllOutputs_AndReportsNewRowCounts()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"i4twins-test-{Guid.NewGuid():N}.db");
        try
        {
            var result = new SqliteProcessingStore(databasePath).Persist(Batch());

            Assert.Equal(2, result.NewReadings);
            Assert.Equal(4, result.NewRuleResults);
            Assert.Equal(2, result.NewEpisodes);
            Assert.Equal(1, result.NewAlerts);
            Assert.Equal(2, CountRows(databasePath, "readings"));
            Assert.Equal(4, CountRows(databasePath, "rule_results"));
            Assert.Equal(2, CountRows(databasePath, "episodes"));
            Assert.Equal(1, CountRows(databasePath, "alerts"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void Persist_TheSameBatchTwice_InsertsNothingTheSecondTime()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"i4twins-test-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteProcessingStore(databasePath);
            store.Persist(Batch());

            var second = store.Persist(Batch());

            Assert.Equal(PersistenceResult.None, second);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void Persist_WithOverlappingInput_OnlyInsertsTheNewRows()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"i4twins-test-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteProcessingStore(databasePath);
            store.Persist(Batch());

            var extra = new ProcessingBatch(
            [
                Evaluation(Reading(70, "2025-06-01T08:01:00Z", 2), acceptable: true,
                    RuleResult.NotViolated("R1", "high temperature")),
                Evaluation(Reading(60, "2025-06-01T08:02:00Z", 3), acceptable: true,
                    RuleResult.NotViolated("R1", "high temperature")),
            ], [], []);

            var result = store.Persist(extra);

            Assert.Equal(1, result.NewReadings);
            Assert.Equal(1, result.NewRuleResults);
            Assert.Equal(0, result.NewEpisodes);
            Assert.Equal(0, result.NewAlerts);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void Persist_AcrossSeparateStoreInstances_SharesTheSameDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"i4twins-test-{Guid.NewGuid():N}.db");
        try
        {
            new SqliteProcessingStore(databasePath).Persist(Batch());
            var second = new SqliteProcessingStore(databasePath).Persist(Batch());

            Assert.Equal(PersistenceResult.None, second);
            Assert.Equal(2, CountRows(databasePath, "readings"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }
}
