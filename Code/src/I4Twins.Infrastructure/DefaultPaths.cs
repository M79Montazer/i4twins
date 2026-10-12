namespace I4Twins.Infrastructure;

public static class DefaultPaths
{
    public static string UnderSolutionData(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "I4Twins.sln")))
        {
            dir = dir.Parent!;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                "could not locate the solution directory (I4Twins.sln) above the application base directory");
        }

        return Path.Combine(dir.FullName, "data", fileName);
    }
}
