using I4Twins.Application;

namespace I4Twins.Infrastructure.Logging;

public sealed class ConsoleProcessingLog : IProcessingLog
{
    public void Debug(string message)
    {
    }

    public void Information(string message) => Console.WriteLine($"[INFO] {message}");

    public void Warning(string message) => Console.WriteLine($"[WARN] {message}");

    public void Error(string message) => Console.Error.WriteLine($"[ERROR] {message}");
}
