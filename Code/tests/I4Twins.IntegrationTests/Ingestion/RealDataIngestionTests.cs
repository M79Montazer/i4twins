using I4Twins.Application.Ingestion;
using I4Twins.Infrastructure.Input;
using Xunit.Abstractions;

namespace I4Twins.IntegrationTests.Ingestion;


public class RealDataIngestionTests(ITestOutputHelper output)
{
    private static string FindDataFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "I4Twins.sln")))
        {
            dir = dir.Parent!;
        }

        var path = Path.Combine(dir!.FullName, "data", "readings.jsonl");
        Assert.True(File.Exists(path), $"input data not found at {path}");
        return path;
    }

    [Fact]
    public void RealDataset_ProducesConsistentCounters_WithoutCrashing()
    {
        var service = new IngestionService(
            new FileLineSource(FindDataFile()),
            new JsonLineParser());

        var outcome = service.Run();
        var c = outcome.Counters;

        output.WriteLine($"Total lines read:        {c.TotalLines}");
        output.WriteLine($"Parsed readings:         {c.ParsedReadings}");
        output.WriteLine($"Stored readings:         {c.StoredReadings}");
        output.WriteLine($"Duplicates removed:      {c.DuplicatesRemoved}");
        output.WriteLine($"Invalid records rejected:{c.InvalidRecords} (malformed: {c.MalformedLines}, invalid fields: {c.ValidationRejected})");

        Assert.Equal(2150, c.TotalLines);
        Assert.Equal(1, c.MalformedLines);
        Assert.Equal(8, c.ValidationRejected);
        Assert.Equal(2149, c.ParsedReadings);
        Assert.Equal(c.TotalLines, c.StoredReadings + c.DuplicatesRemoved + c.InvalidRecords);
        Assert.True(c.DuplicatesRemoved > 0, "the supplied dataset is known to contain duplicates");
        Assert.True(c.StoredReadings > 2000);
    }
}
