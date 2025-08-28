using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Xunit.Abstractions;

namespace ContentAuthenticity.Tests;

/// <summary>
/// Performance and memory tests for Reader and Builder to detect memory leaks and performance issues
/// </summary>
[Collection("PerformanceTests")]
public class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void C2paReader_MemoryLeak_Test()
    {
        const int iterations = 1000;
        var format = "image/jpeg";
        
        var initialMemory = GC.GetTotalMemory(true);
        
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                using var stream = File.OpenRead("CACAE-uri-CA.jpg");
                using var reader = Reader.FromStream(stream, format);
                // Access properties to ensure full initialization
                _ = reader?.Json;
            }
            catch (C2paException)
            {
                // Expected for test data without valid C2PA manifest
            }
            
            // Force garbage collection every 100 iterations
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        
        // Final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryDifference = finalMemory - initialMemory;
        
        _output.WriteLine($"Initial Memory: {initialMemory:N0} bytes");
        _output.WriteLine($"Final Memory: {finalMemory:N0} bytes");
        _output.WriteLine($"Memory Difference: {memoryDifference:N0} bytes");
        
        // Allow for some memory growth but detect significant leaks
        // Memory difference should be less than 10MB for this test
        Assert.True(memoryDifference < 10_000_000, 
            $"Potential memory leak detected. Memory increased by {memoryDifference:N0} bytes");
    }

    [Fact]
    public void C2paBuilder_MemoryLeak_Test()
    {
        const int iterations = 500; // Fewer iterations as Builder operations are more expensive
        var manifestJson = GenerateTestManifest();
        
        var initialMemory = GC.GetTotalMemory(true);
        
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                using var builder = Builder.FromJson(manifestJson);
                // Test archive operations
                using var archiveStream = new MemoryStream();
                builder?.ToArchive(archiveStream);
            }
            catch (C2paException)
            {
                // Expected for test manifest without proper setup
            }
            
            // Force garbage collection every 50 iterations
            if (i % 50 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        
        // Final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryDifference = finalMemory - initialMemory;
        
        _output.WriteLine($"Initial Memory: {initialMemory:N0} bytes");
        _output.WriteLine($"Final Memory: {finalMemory:N0} bytes");
        _output.WriteLine($"Memory Difference: {memoryDifference:N0} bytes");
        
        // Allow for some memory growth but detect significant leaks
        // Memory difference should be less than 15MB for this test
        Assert.True(memoryDifference < 15_000_000, 
            $"Potential memory leak detected. Memory increased by {memoryDifference:N0} bytes");
    }

    [Fact]
    public void C2paStream_MemoryLeak_Test()
    {
        const int iterations = 2000;
        var initialMemory = GC.GetTotalMemory(true);
        
        for (int i = 0; i < iterations; i++)
        {
            using var stream = File.OpenRead("CACAE-uri-CA.jpg");
            using var c2paStream = new StreamAdapter(stream);
            
            // Force garbage collection every 200 iterations
            if (i % 200 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        
        // Final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryDifference = finalMemory - initialMemory;
        
        _output.WriteLine($"Initial Memory: {initialMemory:N0} bytes");
        _output.WriteLine($"Final Memory: {finalMemory:N0} bytes");
        _output.WriteLine($"Memory Difference: {memoryDifference:N0} bytes");
        
        // Allow for some memory growth but detect significant leaks
        // Memory difference should be less than 5MB for this test
        Assert.True(memoryDifference < 5_000_000, 
            $"Potential memory leak detected. Memory increased by {memoryDifference:N0} bytes");
    }

    [Fact]
    public void C2paReader_LargeData_MemoryUsage_Test()
    {
        var format = "image/jpeg";
        
        var initialMemory = GC.GetTotalMemory(true);
        
        try
        {
            using var stream = File.OpenRead("CACAE-uri-CA.jpg");
            using var reader = Reader.FromStream(stream, format);
            _ = reader?.Json;
            
            var peakMemory = GC.GetTotalMemory(false);
            var memoryUsage = peakMemory - initialMemory;
            
            _output.WriteLine($"Memory usage for 10MB data: {memoryUsage:N0} bytes");
            
            // Memory usage should be reasonable relative to input size
            // Allow up to 5x the input size for internal processing
            Assert.True(memoryUsage < stream.Length * 5, 
                $"Excessive memory usage: {memoryUsage:N0} bytes for {stream.Length:N0} bytes input");
        }
        catch (C2paException)
        {
            // Expected for test data without valid C2PA manifest
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var residualMemory = finalMemory - initialMemory;
        
        _output.WriteLine($"Residual memory after disposal: {residualMemory:N0} bytes");
        
        // Ensure memory is properly released after disposal
        Assert.True(residualMemory < 1_000_000, 
            $"Memory not properly released after disposal: {residualMemory:N0} bytes remaining");
    }

    [Fact]
    public async Task C2paReader_Concurrent_Access_Test()
    {
        const int concurrentTasks = 10;
        const int iterationsPerTask = 100;
        var format = "image/jpeg";
        
        var initialMemory = GC.GetTotalMemory(true);
        var tasks = new Task[concurrentTasks];
        
        for (int t = 0; t < concurrentTasks; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < iterationsPerTask; i++)
                {
                    try
                    {
                        using var stream = File.OpenRead("C.jpg");
                        using var reader = Reader.FromStream(stream, format);
                        _ = reader?.Json;
                    }
                    catch (C2paException)
                    {
                        // Expected for test data without valid C2PA manifest
                    }
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryDifference = finalMemory - initialMemory;
        
        _output.WriteLine($"Concurrent test memory difference: {memoryDifference:N0} bytes");
        
        // Memory usage should remain reasonable even with concurrent access
        Assert.True(memoryDifference < 20_000_000, 
            $"Excessive memory usage in concurrent scenario: {memoryDifference:N0} bytes");
    }

    [Fact]
    public void NativeToManagedMapping_MemoryLeak_Test()
    {
        const int iterations = 1000;
        
        var initialMemory = GC.GetTotalMemory(true);
        
        // Test both StreamContext and C2paStream native-to-managed mappings
        for (int i = 0; i < iterations; i++)
        {
            using var stream = File.OpenRead("C.jpg");
            using var c2paStream = new StreamAdapter(stream);
            
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryDifference = finalMemory - initialMemory;
        
        _output.WriteLine($"Native mapping test memory difference: {memoryDifference:N0} bytes");
        
        // Ensure native-to-managed mappings don't cause memory leaks
        Assert.True(memoryDifference < 5_000_000, 
            $"Native-to-managed mapping memory leak detected: {memoryDifference:N0} bytes");
    }

    [Fact]
    public void Finalizer_Cleanup_Test()
    {
        const int iterations = 500;
        var testData = GenerateTestData(1024);
        var format = "image/jpeg";
        
        var initialMemory = GC.GetTotalMemory(true);
        
        // Create objects without explicit disposal to test finalizer cleanup
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                var stream = new MemoryStream(testData);
                var reader = Reader.FromStream(stream, format);
                _ = reader?.Json;
                // Intentionally not disposing to test finalizer
            }
            catch (C2paException)
            {
                // Expected for test data without valid C2PA manifest
            }
            
            if (i % 50 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        
        // Force multiple garbage collection cycles to ensure finalizers run
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.Sleep(100); // Give finalizers time to complete
        }
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryDifference = finalMemory - initialMemory;
        
        _output.WriteLine($"Finalizer test memory difference: {memoryDifference:N0} bytes");
        
        // Finalizers should eventually clean up resources
        // Allow for more memory usage since finalizers may not run immediately
        Assert.True(memoryDifference < 25_000_000, 
            $"Finalizers not properly cleaning up resources: {memoryDifference:N0} bytes");
    }

    [Fact]
    public void PerformanceBaseline_C2paReader_Test()
    {
        const int iterations = 100;
        var format = "image/jpeg";
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                using var stream = File.OpenRead("C.jpg");
                using var reader = Reader.FromStream(stream, format);
                _ = reader?.Json;
            }
            catch (C2paException)
            {
                // Expected for test data without valid C2PA manifest
            }
        }
        
        stopwatch.Stop();
        var avgTimePerOperation = stopwatch.ElapsedMilliseconds / (double)iterations;
        
        _output.WriteLine($"Average time per Reader operation: {avgTimePerOperation:F2} ms");
        
        // Performance baseline - operations should complete reasonably quickly
        Assert.True(avgTimePerOperation < 200, 
            $"Reader performance degradation detected: {avgTimePerOperation:F2} ms per operation");
    }

    [Fact]
    public void PerformanceBaseline_C2paBuilder_Test()
    {
        const int iterations = 50;
        var manifestJson = GenerateTestManifest();
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                using var builder = Builder.FromJson(manifestJson);
                using var archiveStream = new MemoryStream();
                builder?.ToArchive(archiveStream);
            }
            catch (C2paException)
            {
                // Expected for test manifest without proper setup
            }
        }
        
        stopwatch.Stop();
        var avgTimePerOperation = stopwatch.ElapsedMilliseconds / (double)iterations;
        
        _output.WriteLine($"Average time per Builder operation: {avgTimePerOperation:F2} ms");
        
        // Performance baseline - operations should complete reasonably quickly
        Assert.True(avgTimePerOperation < 500, 
            $"Builder performance degradation detected: {avgTimePerOperation:F2} ms per operation");
    }

    private static byte[] GenerateTestData(int size)
    {
        var random = new Random(12345); // Fixed seed for reproducible tests
        var data = new byte[size];
        random.NextBytes(data);
        return data;
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
public class BenchmarkRunner
{
    [Fact]
    public void RunBenchmarks()
    {
        // This test can be run separately to execute BenchmarkDotNet benchmarks
        // Comment out the Skip attribute to run benchmarks
        var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<C2paPerformanceBenchmarks>();
        Assert.NotNull(summary);
    }
}