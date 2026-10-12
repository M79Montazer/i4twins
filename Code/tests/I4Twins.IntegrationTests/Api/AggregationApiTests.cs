using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using I4Twins.Application;
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

namespace I4Twins.IntegrationTests.Api;

public class AggregationApiTests
{
    private static readonly string RulesJson = """
        [
          {
            "id": "R1",
            "name": "High Temperature",
            "metric": "temperature",
            "operator": "GreaterThan",
            "threshold": 72,
            "enabled": true
          }
        ]
        """;

    private static readonly string ReadingsJsonl = string.Join('\n',
        """{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:00:00Z","value":20,"seq":1}""",
        """{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:04:59Z","value":25,"seq":2}""",
        """{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:05:00Z","value":30,"seq":3}""",
        """{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:09:59Z","value":90,"seq":4}""",
        """{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:10:00Z","value":22,"seq":5}""",
        """{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:14:59Z","value":24,"seq":6}""",
        """{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:15:00Z","value":50,"seq":7}""",
        """{"deviceId":"PUMP-02","metric":"temperature","ts":"2025-06-01T08:00:10Z","value":99,"seq":1}""",
        """{"deviceId":"PUMP-01","metric":"pressure","ts":"2025-06-01T08:00:20Z","value":10,"seq":8}""");

    [Fact]
    public async Task Aggregations_ReturnBucketsOverAcceptableReadingsOnly()
    {
        using var setup = await WithProcessedDatabaseAsync();
        var client = setup.Client;

        var response = await client.GetAsync(
            "/api/aggregations?deviceId=PUMP-01&metric=temperature" +
            "&from=2025-06-01T08:00:00Z&to=2025-06-01T08:15:00Z&bucketSeconds=300");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PUMP-01", json.GetProperty("deviceId").GetString());
        Assert.Equal("temperature", json.GetProperty("metric").GetString());
        Assert.Equal(300, json.GetProperty("bucketSeconds").GetInt32());

        var buckets = json.GetProperty("buckets");
        Assert.Equal(3, buckets.GetArrayLength());

        Assert.Equal(ParseTs("2025-06-01T08:00:00Z"), ParseTs(buckets[0].GetProperty("startTs").GetString()!));
        Assert.Equal(2, buckets[0].GetProperty("count").GetInt32());
        Assert.Equal(22.5, buckets[0].GetProperty("average").GetDouble());
        Assert.Equal(20, buckets[0].GetProperty("min").GetDouble());
        Assert.Equal(25, buckets[0].GetProperty("max").GetDouble());

        Assert.Equal(ParseTs("2025-06-01T08:05:00Z"), ParseTs(buckets[1].GetProperty("startTs").GetString()!));
        Assert.Equal(1, buckets[1].GetProperty("count").GetInt32());
        Assert.Equal(30, buckets[1].GetProperty("average").GetDouble());

        Assert.Equal(2, buckets[2].GetProperty("count").GetInt32());
        Assert.Equal(23, buckets[2].GetProperty("average").GetDouble());
    }

    [Fact]
    public async Task Aggregations_WithNoReadingsInRange_ReturnEmptyBucketList()
    {
        using var setup = await WithProcessedDatabaseAsync();
        var client = setup.Client;

        var response = await client.GetAsync(
            "/api/aggregations?deviceId=PUMP-01&metric=temperature" +
            "&from=2025-06-01T09:00:00Z&to=2025-06-01T10:00:00Z&bucketSeconds=300");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, json.GetProperty("buckets").GetArrayLength());
    }

    [Fact]
    public async Task Aggregations_WithDeviceOrMetricFilter_ReturnsOnlyMatchingReadings()
    {
        using var setup = await WithProcessedDatabaseAsync();
        var client = setup.Client;

        var response = await client.GetAsync(
            "/api/aggregations?deviceId=PUMP-02&metric=temperature" +
            "&from=2025-06-01T08:00:00Z&to=2025-06-01T09:00:00Z&bucketSeconds=600");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, json.GetProperty("buckets").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/aggregations?metric=temperature&from=2025-06-01T08:00:00Z&to=2025-06-01T09:00:00Z&bucketSeconds=300")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&from=2025-06-01T08:00:00Z&to=2025-06-01T09:00:00Z&bucketSeconds=300")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&to=2025-06-01T09:00:00Z&bucketSeconds=300")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&from=2025-06-01T08:00:00Z&bucketSeconds=300")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&from=2025-06-01T08:00:00Z&to=2025-06-01T09:00:00Z")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&from=2025-06-01T08:00:00Z&to=2025-06-01T09:00:00Z&bucketSeconds=0")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&from=2025-06-01T08:00:00Z&to=2025-06-01T09:00:00Z&bucketSeconds=-10")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&from=not-a-time&to=2025-06-01T09:00:00Z&bucketSeconds=300")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&from=2025-06-01T08:00:00Z&to=2025-06-01T09:00:00Z&bucketSeconds=abc")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&from=2025-06-01T09:00:00Z&to=2025-06-01T08:00:00Z&bucketSeconds=300")]
    [InlineData("/api/aggregations?deviceId=PUMP-01&metric=temperature&from=2025-06-01T08:00:00Z&to=2025-06-01T08:00:00Z&bucketSeconds=300")]
    public async Task Aggregations_WithInvalidParameters_ReturnClientError(string requestUri)
    {
        using var setup = await WithProcessedDatabaseAsync();
        var client = setup.Client;

        var response = await client.GetAsync(requestUri);

        Assert.Equal(400, (int)response.StatusCode);
    }

    private static DateTimeOffset ParseTs(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static async Task<ApiSetup> WithProcessedDatabaseAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"i4twins-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var rulesPath = Path.Combine(tempDir, "rules.json");
        var readingsPath = Path.Combine(tempDir, "readings.jsonl");
        var databasePath = Path.Combine(tempDir, "test.db");
        await File.WriteAllTextAsync(rulesPath, RulesJson);
        await File.WriteAllTextAsync(readingsPath, ReadingsJsonl);

        var registry = RuleOperatorRegistry.WithDefaultOperators();
        var pipeline = new ProcessingPipeline(
            new RuleSetService(new FileRuleSource(rulesPath), new JsonRuleParser(), registry),
            new IngestionService(new FileLineSource(readingsPath), new JsonLineParser()),
            new RuleEvaluationService(new RuleEvaluator(registry), registry),
            new AlertService(),
            new SqliteProcessingStore(databasePath));
        pipeline.Run();

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Database:Path", databasePath));
        return new ApiSetup(factory, tempDir);
    }

    private sealed class ApiSetup(WebApplicationFactory<Program> factory, string tempDir) : IDisposable
    {
        public HttpClient Client { get; } = factory.CreateClient();

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            factory.Dispose();
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
