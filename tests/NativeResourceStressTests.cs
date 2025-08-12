using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Microsot.ContentAuthenticity.BindingTests;

/// <summary>
/// Stress tests focusing on native resource management and disposal patterns
/// </summary>
public class NativeResourceStressTests
{
    private readonly ITestOutputHelper _output;

    public NativeResourceStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void NativeMappingDictionary_GrowthAndCleanup_Test()
    {
        const int iterations = 5000;
        var testData = GenerateTestData(512);
        
        // Test that NativeToManagedMap dictionaries don't grow indefinitely
        var initialMemory = GC.GetTotalMemory(true);
        
        for (int i = 0; i < iterations; i++)
        {
            using var stream = new MemoryStream(testData);
            using var c2paStream = new C2paStream(stream);
            
            // Access context to ensure mapping is created
            var context = c2paStream.Context;
            Assert.NotNull(context);
            
            // Every 500 iterations, check if mappings are being cleaned up
            if (i % 500 == 0 && i > 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var currentMemory = GC.GetTotalMemory(false);
                var memoryGrowth = currentMemory - initialMemory;
                
                _output.WriteLine($"Iteration {i}: Memory growth: {memoryGrowth:N0} bytes");
                
                // Memory growth should be bounded
                Assert.True(memoryGrowth < 50_000_000, 
                    $"Excessive memory growth at iteration {i}: {memoryGrowth:N0} bytes");
            }
        }
        
        // Final cleanup and verification
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var totalGrowth = finalMemory - initialMemory;
        
        _output.WriteLine($"Final memory growth: {totalGrowth:N0} bytes");
        Assert.True(totalGrowth < 20_000_000, 
            $"Native mapping dictionaries not properly cleaned up: {totalGrowth:N0} bytes");
    }

    [Fact]
    public void DisposalPattern_ExceptionSafety_Test()
    {
        const int iterations = 1000;
        var testData = GenerateTestData(1024);
        var format = "image/jpeg";
        var exceptions = 0;
        
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                using var stream = new MemoryStream(testData);
                
                // Test disposal during various states of initialization
                if (i % 3 == 0)
                {
                    // Test early disposal
                    var reader = C2paReader.FromStream(stream, format);
                    reader.Dispose(); // Explicit disposal
                }
                else if (i % 3 == 1)
                {
                    // Test disposal after property access
                    using var reader = C2paReader.FromStream(stream, format);
                    try
                    {
                        _ = reader?.Json;
                    }
                    catch (C2paException)
                    {
                        exceptions++;
                    }
                }
                else
                {
                    // Test double disposal
                    var reader = C2paReader.FromStream(stream, format);
                    reader?.Dispose();
                    reader?.Dispose(); // Should not throw
                }
            }
            catch (C2paException)
            {
                exceptions++;
            }
            catch (ObjectDisposedException)
            {
                // This should not happen in properly written disposal code
                Assert.True(false, "ObjectDisposedException thrown - disposal pattern issue detected");
            }
        }
        
        _output.WriteLine($"Total C2paExceptions (expected): {exceptions}");
        
        // Ensure we can handle disposal in various states without crashes
        Assert.True(exceptions > 0, "Expected some C2paExceptions for invalid test data");
    }

    [Fact]
    public void HighFrequency_CreateDispose_Test()
    {
        const int iterations = 10000;
        var testData = GenerateTestData(256);
        
        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(true);
        
        // Rapid creation and disposal to stress the native resource management
        for (int i = 0; i < iterations; i++)
        {
            using var stream = new MemoryStream(testData);
            using var c2paStream = new C2paStream(stream);
            
            // Quick operations to test rapid allocation/deallocation
            _ = c2paStream.Context;
            
            if (i % 1000 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        
        stopwatch.Stop();
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryDifference = finalMemory - initialMemory;
        var avgTimePerOp = stopwatch.ElapsedMilliseconds / (double)iterations;
        
        _output.WriteLine($"High frequency test completed in {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average time per operation: {avgTimePerOp:F3} ms");
        _output.WriteLine($"Memory difference: {memoryDifference:N0} bytes");
        
        // Should complete quickly without significant memory leaks
        Assert.True(avgTimePerOp < 1.0, $"Operations too slow: {avgTimePerOp:F3} ms per operation");
        Assert.True(memoryDifference < 10_000_000, $"Memory leak detected: {memoryDifference:N0} bytes");
    }

    [Fact]
    public void CrossThread_NativeResourceAccess_Test()
    {
        const int threadCount = 8;
        const int iterationsPerThread = 500;
        var testData = GenerateTestData(1024);
        var format = "image/jpeg";
        
        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();
        
        for (int t = 0; t < threadCount; t++)
        {
            var threadId = t;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        try
                        {
                            using var stream = new MemoryStream(testData);
                            using var reader = C2paReader.FromStream(stream, format);
                            _ = reader?.Json;
                            
                            // Test concurrent access to native-to-managed mappings
                            using var c2paStream = new C2paStream(stream);
                            _ = c2paStream.Context;
                        }
                        catch (C2paException)
                        {
                            // Expected for test data
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        // Should not have any unexpected exceptions from multi-threading
        var unexpectedExceptions = exceptions.Where(ex => !(ex is C2paException)).ToList();
        
        foreach (var ex in unexpectedExceptions)
        {
            _output.WriteLine($"Unexpected exception: {ex}");
        }
        
        Assert.Empty(unexpectedExceptions);
        _output.WriteLine($"Cross-thread test completed with {exceptions.Count} expected C2paExceptions");
    }

    [Fact]
    public void LongRunning_MemoryPressure_Test()
    {
        const int cycles = 10;
        const int iterationsPerCycle = 1000;
        var testData = GenerateTestData(2048);
        var format = "image/jpeg";
        
        var memoryReadings = new List<long>();
        var initialMemory = GC.GetTotalMemory(true);
        memoryReadings.Add(initialMemory);
        
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            _output.WriteLine($"Starting cycle {cycle + 1}/{cycles}");
            
            for (int i = 0; i < iterationsPerCycle; i++)
            {
                try
                {
                    using var stream = new MemoryStream(testData);
                    using var reader = C2paReader.FromStream(stream, format);
                    _ = reader?.Json;
                    
                    using var c2paStream = new C2paStream(stream);
                    _ = c2paStream.Context;
                }
                catch (C2paException)
                {
                    // Expected for test data
                }
            }
            
            // Force garbage collection between cycles
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var currentMemory = GC.GetTotalMemory(false);
            memoryReadings.Add(currentMemory);
            
            var memoryGrowthSinceStart = currentMemory - initialMemory;
            _output.WriteLine($"Cycle {cycle + 1} memory: {currentMemory:N0} bytes (growth: {memoryGrowthSinceStart:N0})");
            
            // Check for excessive memory growth over time
            Assert.True(memoryGrowthSinceStart < 100_000_000, 
                $"Excessive memory growth in cycle {cycle + 1}: {memoryGrowthSinceStart:N0} bytes");
        }
        
        // Analyze memory trend
        var finalMemory = memoryReadings.Last();
        var totalGrowth = finalMemory - initialMemory;
        var avgGrowthPerCycle = memoryReadings.Zip(memoryReadings.Skip(1))
            .Select(pair => pair.Second - pair.First)
            .Average();
        
        _output.WriteLine($"Total memory growth: {totalGrowth:N0} bytes");
        _output.WriteLine($"Average growth per cycle: {avgGrowthPerCycle:F0} bytes");
        
        // Memory growth should be reasonable and not indicate severe leaks
        Assert.True(totalGrowth < 50_000_000, 
            $"Long-running test shows significant memory growth: {totalGrowth:N0} bytes");
        Assert.True(avgGrowthPerCycle < 5_000_000, 
            $"Average memory growth per cycle too high: {avgGrowthPerCycle:F0} bytes");
    }

    [Fact]
    public void NativeCallback_MemoryManagement_Test()
    {
        const int iterations = 1000;
        var testData = GenerateTestData(1024);
        
        var initialMemory = GC.GetTotalMemory(true);
        
        // Test that callback delegates don't cause memory leaks
        for (int i = 0; i < iterations; i++)
        {
            using var stream = new MemoryStream(testData);
            using var c2paStream = new C2paStream(stream);
            
            // Access all callback properties to ensure they're created
            var reader = c2paStream.Reader;
            var writer = c2paStream.Writer;
            var seeker = c2paStream.Seeker;
            var flusher = c2paStream.Flusher;
            
            Assert.NotNull(reader);
            Assert.NotNull(writer);
            Assert.NotNull(seeker);
            Assert.NotNull(flusher);
            
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
        
        _output.WriteLine($"Native callback test memory difference: {memoryDifference:N0} bytes");
        
        // Ensure callback delegates don't cause memory leaks
        Assert.True(memoryDifference < 10_000_000, 
            $"Native callback memory leak detected: {memoryDifference:N0} bytes");
    }

    private static byte[] GenerateTestData(int size)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        var data = new byte[size];
        random.NextBytes(data);
        return data;
    }
}