using BenchmarkDotNet.Attributes;

namespace ContentAuthenticity.Tests;

/// <summary>
/// BenchmarkDotNet benchmarks for more detailed performance analysis
/// Run with: dotnet run --configuration Release --project tests/ -- --filter "*Benchmarks*"
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class C2paPerformanceBenchmarks
{
    private readonly string _format = "image/jpeg";
    private readonly string _manifestJson = GenerateTestManifest();

    [Benchmark]
    public void C2paReader_FromStream_Benchmark()
    {
        try
        {
            using var stream = File.OpenRead("C.jpg");
            using var reader = Reader.FromStream(stream, _format);
            _ = reader?.Json;
        }
        catch (C2paException)
        {
            // Expected for test data
        }
    }

    [Benchmark]
    public void C2paBuilder_FromJson_Benchmark()
    {
        try
        {
            using var builder = Builder.FromJson(_manifestJson);
            using var archiveStream = new MemoryStream();
            builder?.ToArchive(archiveStream);
        }
        catch (C2paException)
        {
            // Expected for test manifest
        }
    }

    private static string GenerateTestManifest()
    {
        return """
    {
        "claim_generator": "test-generator/1.0",
        "claim_generator_info": [
            {
                "name": "test-generator",
                "version": "1.0"
            }
        ],
        "assertions": [
            {
                "label": "c2pa.actions",
                "data": {
                    "actions": [
                        {
                            "action": "c2pa.created"
                        }
                    ]
                }
            }
        ]
    }
    """;
    }
}

/// <summary>
/// Helper class to run BenchmarkDotNet tests
/// </summary>
[Collection("PerformanceTests")]
public class BenchmarkTests
{
    [Fact]
    public void RunBenchmarks()
    {
        // This test can be run separately to execute BenchmarkDotNet benchmarks
        // Comment out the Skip attribute to run benchmarks
        var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<C2paPerformanceBenchmarks>();
        Console.WriteLine(summary);
        Assert.NotNull(summary);
    }
}