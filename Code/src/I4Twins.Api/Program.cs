using System.Globalization;
using I4Twins.Application.Aggregation;
using I4Twins.Infrastructure;
using I4Twins.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var databasePath = builder.Configuration["Database:Path"] ?? DefaultPaths.UnderSolutionData("i4twins.db");
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
