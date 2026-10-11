using I4Twins.Application;
using I4Twins.Application.Alerts;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Persistence;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Infrastructure.Input;
using I4Twins.Infrastructure.Persistence;
using I4Twins.Infrastructure.Rules;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace I4Twins.IntegrationTests.Persistence;

public class RealDataIdempotencyTests(ITestOutputHelper output)
{
    private static ProcessingPipeline NewPipelineExecution(string databasePath) => new(
        new RuleSetService(
            new FileRuleSource(TestData.DataFile("rules.json")),
            new JsonRuleParser(),
            RuleOperatorRegistry.WithDefaultOperators()),
        new IngestionService(
            new FileLineSource(TestData.DataFile("readings.jsonl")),
            new JsonLineParser()),
        new RuleEvaluationService(
            new RuleEvaluator(RuleOperatorRegistry.WithDefaultOperators()),
            RuleOperatorRegistry.WithDefaultOperators()),
        new AlertService(),
        new SqliteProcessingStore(databasePath));

    private static long CountRows(string databasePath, string table)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void ReProcessingTheSameInput_AcrossSeparateRuns_InsertsNothingNew()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"i4twins-e2e-{Guid.NewGuid():N}.db");
        try
        {
            var first = NewPipelineExecution(databasePath).Run();
            var persistence = first.Persistence;
            output.WriteLine($"Run 1 inserted: {persistence.NewReadings} readings, {persistence.NewRuleResults} rule results, " +
                             $"{persistence.NewEpisodes} episodes, {persistence.NewAlerts} alerts");

            Assert.Equal(2103, persistence.NewReadings);
            Assert.Equal(1680, persistence.NewRuleResults);
            Assert.Equal(4, persistence.NewEpisodes);
            Assert.Equal(1, persistence.NewAlerts);

            var afterFirst = (
                Readings: CountRows(databasePath, "readings"),
                RuleResults: CountRows(databasePath, "rule_results"),
                Episodes: CountRows(databasePath, "episodes"),
                Alerts: CountRows(databasePath, "alerts"));

            var second = NewPipelineExecution(databasePath).Run();
            output.WriteLine($"Run 2 inserted: {second.Persistence.NewRows} rows in total");

            Assert.Equal(PersistenceResult.None, second.Persistence);
            Assert.Equal(afterFirst.Readings, CountRows(databasePath, "readings"));
            Assert.Equal(afterFirst.RuleResults, CountRows(databasePath, "rule_results"));
            Assert.Equal(afterFirst.Episodes, CountRows(databasePath, "episodes"));
            Assert.Equal(afterFirst.Alerts, CountRows(databasePath, "alerts"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }
}
