using I4Twins.Application.Aggregation;
using I4Twins.Domain.Readings;
using Microsoft.Data.Sqlite;

namespace I4Twins.Infrastructure.Persistence;

public sealed class SqliteAggregationSource(string databasePath) : IAcceptableReadingSource
{
    public IReadOnlyList<SensorReading> ReadAcceptable(string deviceId, string metric, DateTimeOffset from, DateTimeOffset to)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ts, seq, value
            FROM readings
            WHERE device_id = $device_id AND metric = $metric AND acceptable = 1
              AND ts >= $from AND ts < $to
            ORDER BY ts, seq
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$metric", metric);
        command.Parameters.AddWithValue("$from", Timestamp.Encode(from));
        command.Parameters.AddWithValue("$to", Timestamp.Encode(to));

        var readings = new List<SensorReading>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            readings.Add(new SensorReading(
                deviceId,
                metric,
                DateTimeOffset.Parse(reader.GetString(0), styles: System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetDouble(2),
                reader.GetInt64(1)));
        }

        return readings;
    }
}
