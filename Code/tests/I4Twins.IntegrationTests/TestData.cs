namespace I4Twins.IntegrationTests;

public static class TestData
{
    private static string SolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "I4Twins.sln")))
        {
            dir = dir.Parent!;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    public static string DataFile(string name)
    {
        var path = Path.Combine(SolutionDirectory(), "data", name);
        Assert.True(File.Exists(path), $"test data not found at {path}");
        return path;
    }
}
