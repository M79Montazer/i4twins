using I4Twins.Domain.Readings;

namespace I4Twins.Application.Ingestion;

public interface ILineSource
{
    IEnumerable<string> ReadLines();
}

public interface ILineParser
{
    LineParseResult Parse(string line);
}
