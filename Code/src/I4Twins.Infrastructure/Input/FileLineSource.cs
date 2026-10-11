using I4Twins.Application.Ingestion;

namespace I4Twins.Infrastructure.Input;

public sealed class FileLineSource(string path) : ILineSource
{
    public IEnumerable<string> ReadLines() => File.ReadLines(path);
}
