using System.Globalization;
using I4Twins.Application;
using I4Twins.Application.Aggregation;
using I4Twins.Application.Alerts;
using I4Twins.Application.Ingestion;
using I4Twins.Application.Persistence;
using I4Twins.Application.Rules;
using I4Twins.Domain.Rules;
using I4Twins.Infrastructure;
using I4Twins.Infrastructure.Input;
using I4Twins.Infrastructure.Logging;
using I4Twins.Infrastructure.Persistence;
using I4Twins.Infrastructure.Rules;

var builder = WebApplication.CreateBuilder(args);

var databasePath = builder.Configuration["Database:Path"] ?? DefaultPaths.UnderSolutionData("i4twins.db");
var readingsPath = builder.Configuration["Processing:ReadingsPath"] ?? DefaultPaths.UnderSolutionData("readings.jsonl");
var rulesPath = builder.Configuration["Processing:RulesPath"] ?? DefaultPaths.UnderSolutionData("rules.json");

IProcessingLog log = new ConsoleProcessingLog();
var registry = RuleOperatorRegistry.WithDefaultOperators();
var pipeline = new ProcessingPipeline(
    new RuleSetService(new FileRuleSource(rulesPath), new JsonRuleParser(), registry),
    new IngestionService(new FileLineSource(readingsPath), new JsonLineParser(), log),
    new RuleEvaluationService(new RuleEvaluator(registry), registry, log),
    new AlertService(new CooldownPolicy(), log),
    new SqliteProcessingStore(databasePath),
    log);
var outcome = pipeline.Run();
Console.WriteLine(ProcessingReport.Format(outcome));

builder.Services.AddSingleton<IAcceptableReadingSource>(new SqliteAggregationSource(databasePath));
builder.Services.AddSingleton<AggregationService>();

var app = builder.Build();

app.MapGet("/", () => "i4Twins sensor monitoring API");

app.MapGet("/api/aggregations", (
    string? deviceId,
    string? metric,
    string? from,
    string? to,
    int? bucketSeconds,
    AggregationService aggregation) =>
{
    if (string.IsNullOrWhiteSpace(deviceId))
    {
        return Results.BadRequest(new { error = "deviceId is required" });
    }

    if (string.IsNullOrWhiteSpace(metric))
    {
        return Results.BadRequest(new { error = "metric is required" });
    }

    if (bucketSeconds is null or <= 0)
    {
        return Results.BadRequest(new { error = "bucketSeconds must be a positive integer" });
    }

    if (!TryParseUtc(from, out var fromTs))
    {
        return Results.BadRequest(new { error = "from must be a valid UTC timestamp" });
    }

    if (!TryParseUtc(to, out var toTs))
    {
        return Results.BadRequest(new { error = "to must be a valid UTC timestamp" });
    }

    if (fromTs >= toTs)
    {
        return Results.BadRequest(new { error = "from must be earlier than to" });
    }

    return Results.Ok(aggregation.Aggregate(deviceId, metric, fromTs, toTs, bucketSeconds.Value));
});

app.Run();

static bool TryParseUtc(string? value, out DateTimeOffset timestamp) =>
    DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp);

public partial class Program;
