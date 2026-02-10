using System.Diagnostics;
using System.Security.Cryptography;
using Xunit.Abstractions;

namespace ContentAuthenticity.Tests;

/// <summary>
/// Performance and memory tests for C2paReader and C2paBuilder to detect memory leaks and performance issues
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
#if DEBUG
        const int iterations = 10; // 1/100 of Release
#else
        const int iterations = 1000;
#endif
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
#if DEBUG
        const int iterations = 5; // 1/100 of Release
#else
        const int iterations = 500; // Fewer iterations as Builder operations are more expensive
#endif
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
#if DEBUG
        const int iterations = 20; // 1/100 of Release
#else
        const int iterations = 2000;
#endif
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
#if DEBUG
        const int iterationsPerTask = 1; // 1/100 of Release
#else
        const int iterationsPerTask = 100;
#endif
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
#if DEBUG
        const int iterations = 10; // 1/100 of Release
#else
        const int iterations = 1000;
#endif

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
#if DEBUG
        const int iterations = 5; // 1/100 of Release
#else
        const int iterations = 500;
#endif
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
#if DEBUG
        const int iterations = 1; // 1/100 of Release
#else
        const int iterations = 100;
#endif
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

        _output.WriteLine($"Average time per C2paReader operation: {avgTimePerOperation:F2} ms");

        // Performance baseline - operations should complete reasonably quickly
        Assert.True(avgTimePerOperation < 200,
            $"C2paReader performance degradation detected: {avgTimePerOperation:F2} ms per operation");
    }

    [Fact]
    public void PerformanceBaseline_C2paBuilder_Test()
    {
#if DEBUG
        const int iterations = 1; // 1/100 of Release
#else
        const int iterations = 50;
#endif
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

        _output.WriteLine($"Average time per C2paBuilder operation: {avgTimePerOperation:F2} ms");

        // Performance baseline - operations should complete reasonably quickly
        Assert.True(avgTimePerOperation < 500,
            $"C2paBuilder performance degradation detected: {avgTimePerOperation:F2} ms per operation");
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

    public sealed class TestSigner : ISigner, IDisposable
    {
        private readonly RSA _key;

        public TestSigner()
        {
            var key = File.ReadAllText("certs/rs256.pem");
            _key = RSA.Create();
            _key.ImportFromPem(key);
        }

        public void Dispose()
        {
            _key.Dispose();
        }


        public SigningAlg Alg { get; } = SigningAlg.Ps256;

        public string Certs { get; } = File.ReadAllText("certs/rs256.pub");

        public Uri? TimeAuthorityUrl { get; }

        public bool UseOcsp { get; }

        public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
        {
            var bytes = _key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            bytes.CopyTo(hash);
            return bytes.Length;
        }
    }

#if DEBUG
    private const int IterationCount = 10; // 1/100 of Release
#else
    private const int IterationCount = 1000;
#endif

    [Theory]
    [InlineData("Provenance Memleak test using file API with 1000 iterations", IterationCount, false)]
    [InlineData("Provenance Memleak test using buffer API with 1000 iterations", IterationCount, true)]
    public void CheckAuthoringAndValidationMemLeak(string testDescription, int num_iterations, bool use_buffer_api)
    {
        _output.WriteLine("Running test: {0} {1} times", testDescription, num_iterations);
        string inputFile = "video1_no_manifest.mp4";
        string outputFile = "output.mp4";
        ISigner signer = new TestSigner();
        var inputFileBuffer = use_buffer_api ? File.ReadAllBytes(inputFile) : null;
        string mimeType = inputFile.GetMimeType();

        // Ensure we don't attempt to generate an MP4 thumbnail for MP4 inputs.
        // (Some native builds don't support `video/mp4` thumbnails.)
        var settings = C2pa.Settings.Default;
        Assert.NotNull(settings.Builder);
        Assert.NotNull(settings.Builder.Thumbnail);
        var thumbnailSettings = settings.Builder.Thumbnail!;
        thumbnailSettings.Format = C2pa.ThumbnailFormat.Jpeg;
        C2pa.LoadSettings(settings.ToJson(indented: false));

        var manifest = """
                        {
                            "assertions": [
                                {
                                    "label": "c2pa.actions",
                                    "data": {
                                        "actions": [
                                            { "action": "c2pa.created" }
                                        ]
                                    }
                                }
                            ]
                        }
                """;
        var builder = Builder.FromJson(manifest);

        for (int iter = 0; iter < num_iterations; iter++)
        {
            if (use_buffer_api)
            {
                var inputStream = new MemoryStream(inputFileBuffer!);
                var outputStream = new MemoryStream();
                builder.Sign(signer, inputStream, outputStream, mimeType);
                outputStream.Position = 0;
                var reader = Reader.FromStream(outputStream, mimeType);
                Assert.NotNull(reader.Json);
            }
            else
            {
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }

                builder.Sign(signer, inputFile, outputFile);
                var reader = Reader.FromFile(outputFile);
                Assert.NotNull(reader.Json);
            }
        }
    }
}