namespace I4Twins.Application;

public interface IProcessingLog
{
    void Debug(string message);
    void Information(string message);
    void Warning(string message);
    void Error(string message);
}
