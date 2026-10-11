namespace I4Twins.Infrastructure.Persistence;

public static class Timestamp
{
    public static string Encode(DateTimeOffset timestamp) => timestamp.ToString("O");
}
