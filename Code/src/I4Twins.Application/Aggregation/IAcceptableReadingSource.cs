using I4Twins.Domain.Readings;

namespace I4Twins.Application.Aggregation;

public interface IAcceptableReadingSource
{
    IReadOnlyList<SensorReading> ReadAcceptable(string deviceId, string metric, DateTimeOffset from, DateTimeOffset to);
}
