using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using I4Twins.Application;
using I4Twins.Application.Aggregation;
using I4Twins.Application.Alerts;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Persistence;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Infrastructure.Input;
using I4Twins.Infrastructure.Persistence;
using I4Twins.Infrastructure.Rules;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace I4Twins.IntegrationTests.EndToEnd;

public class EndToEndVerificationTests(ITestOutputHelper output)
{
    private const string ApiUri = "/api/aggregations?deviceId=PUMP-01&metric=temperature" +
        "&from=2025-06-01T08:00:00Z&to=2025-06-01T09:00:00Z&bucketSeconds=300";

    [Fact]
    public async Task CleanDatabase_Restart_Reprocess_IsIdempotent_AndApiMatchesStoredReadings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"i4twins-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var databasePath = Path.Combine(tempDir, "e2e.db");
        try
        {
            string firstApiResponse;
            using (var firstHost = NewHost(databasePath))
            {
                var client = firstHost.CreateClient();
                var response = await client.GetAsync(ApiUri);
                response.EnsureSuccessStatusCode();
                firstApiResponse = await response.Content.ReadAsStringAsync();
                output.WriteLine($"API (execution 1): {firstApiResponse}");
            }

            var afterFirst = DbCounts(databasePath);
            output.WriteLine($"After execution 1: {afterFirst.Readings} readings, {afterFirst.RuleResults} rule results, " +
                             $"{afterFirst.Episodes} episodes, {afterFirst.Alerts} alerts");
            Assert.Equal(2103, afterFirst.Readings);
            Assert.Equal(1680, afterFirst.RuleResults);
            Assert.Equal(4, afterFirst.Episodes);
            Assert.Equal(1, afterFirst.Alerts);

            var firstBuckets = JsonDocument.Parse(firstApiResponse).RootElement.GetProperty("buckets");
            var firstBucket = firstBuckets[0];
            Assert.Equal(ParseTs("2025-06-01T08:00:00Z"), ParseTs(firstBucket.GetProperty("startTs").GetString()!));
            var expected = ExpectedBucketFromDatabase(databasePath, "PUMP-01", "temperature",
                "2025-06-01T08:00:00Z", "2025-06-01T08:05:00Z");
            Assert.Equal(expected.Count, firstBucket.GetProperty("count").GetInt32());
            Assert.Equal(expected.Average, firstBucket.GetProperty("average").GetDouble(), precision: 9);
            Assert.Equal(expected.Min, firstBucket.GetProperty("min").GetDouble(), precision: 9);
            Assert.Equal(expected.Max, firstBucket.GetProperty("max").GetDouble(), precision: 9);

            string secondApiResponse;
            using (var secondHost = NewHost(databasePath))
            {
                var client = secondHost.CreateClient();
                var response = await client.GetAsync(ApiUri);
                response.EnsureSuccessStatusCode();
                secondApiResponse = await response.Content.ReadAsStringAsync();
            }

            var afterSecond = DbCounts(databasePath);
            output.WriteLine($"After execution 2: {afterSecond.Readings} readings, {afterSecond.RuleResults} rule results, " +
                             $"{afterSecond.Episodes} episodes, {afterSecond.Alerts} alerts");
            Assert.Equal(afterFirst, afterSecond);
            Assert.Equal(firstApiResponse, secondApiResponse);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static WebApplicationFactory<Program> NewHost(string databasePath) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Database:Path", databasePath));

    private static (long Readings, long RuleResults, long Episodes, long Alerts) DbCounts(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return (CountRows(connection, "readings"), CountRows(connection, "rule_results"),
            CountRows(connection, "episodes"), CountRows(connection, "alerts"));
    }

    private static long CountRows(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    private static (int Count, double Average, double Min, double Max) ExpectedBucketFromDatabase(
        string databasePath, string deviceId, string metric, string fromIso, string toIso)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT value FROM readings
            WHERE device_id = $device_id AND metric = $metric AND acceptable = 1
              AND ts >= $from AND ts < $to
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$metric", metric);
        command.Parameters.AddWithValue("$from", Timestamp.Encode(ParseTs(fromIso)));
        command.Parameters.AddWithValue("$to", Timestamp.Encode(ParseTs(toIso)));

        var values = new List<double>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                values.Add(reader.GetDouble(0));
            }
        }

        Assert.NotEmpty(values);
        return (values.Count, values.Average(), values.Min(), values.Max());
    }

    private static DateTimeOffset ParseTs(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
