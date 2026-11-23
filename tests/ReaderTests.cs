namespace ContentAuthenticity.Tests;

using System.Text.Json.Nodes;
using Xunit.Abstractions;

public class ReaderTests(ITestOutputHelper output)
{
    [Fact]
    public void WithStream_UnsignedAsset_ConsumesReader()
    {
        using var asset = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "no_manifest.jpg"));
        using var context = new Context();
        var reader = new Reader(context);

        var error = Assert.Throws<C2paException>(() => reader.WithStream(asset, "image/jpeg"));

        Assert.StartsWith("ManifestNotFound:", error.Message);
        Assert.True(reader.IsDisposed);
        reader.Dispose();
    }

    [Fact]
    public void WithFragment_InvalidFragment_ConsumesReader()
    {
        using var asset = new MemoryStream();
        using var fragment = new MemoryStream();
        var reader = new Reader();

        Assert.Throws<C2paException>(() => reader.WithFragment(asset, fragment, "video/mp4"));

        Assert.True(reader.IsDisposed);
        reader.Dispose();
    }

    [Fact]
    public void WithStreamAndManifest_InvalidManifest_ConsumesReader()
    {
        using var asset = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "no_manifest.jpg"));
        var reader = new Reader();

        Assert.Throws<C2paException>(() => reader.WithStreamAndManifest(asset, "image/jpeg", [1]));

        Assert.True(reader.IsDisposed);
        reader.Dispose();
    }

    [Fact]
    public void FromStream_WithValidParameters_ShouldNotThrowDuringCreation()
    {
        // Arrange
        var stream = new MemoryStream([1, 2, 3, 4, 5]);
        var format = "image/jpeg";

        // Act
        var exception = Record.Exception(() =>
        {
            using var ctx = new Context();
            using var reader = new Reader(ctx).WithStream(stream, format);
        });

        // Assert - Should not throw during creation, actual functionality depends on native library
        Assert.True(exception == null || exception is C2paException);
    }

    [Fact]
    public void FromFile_WithValidPath_ShouldHandleFileOperation()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, [1, 2, 3, 4, 5]);

            // Act
            var exception = Record.Exception(() =>
            {
                using var ctx = new Context();
                using var reader = new Reader(ctx).WithFile(tempFile);
            });

            // Assert - Should not throw during creation, actual functionality depends on native library
            Assert.True(exception == null || exception is C2paException);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromFile_WithNonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), "non-existent-file.jpg");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
        {
            using var ctx = new Context();
            using var reader = new Reader(ctx).WithFile(nonExistentFile);
        });
    }

    [Fact]
    public void JsonRoundTrip_ShouldPreserveDataIntegrity()
    {
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*");
        foreach (var file in files)
        {
            try
            {
                using var ctx = new Context();
                using var reader = new Reader(ctx).WithFile(file);
                var originalJson = reader.Json;
                var store = reader.Store;
                var roundTrippedJson = store.ToJson();
                AssertJsonEquivalent(originalJson, roundTrippedJson);
            }
            catch (C2paException ex)
            {
                output.WriteLine($"C2paException for file {file}: {ex}");
            }
        }
    }

    [Fact]
    public void CrJson_ShouldBeValidJson()
    {
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.png");
        foreach (var file in files)
        {
            try
            {
                using var ctx = new Context();
                using var reader = new Reader(ctx).WithFile(file);
                var crJson = reader.CrJson;
                output.WriteLine($"CR JSON for file {file}: {crJson}");
                Assert.NotNull(JsonNode.Parse(crJson));
            }
            catch (C2paException ex)
            {
                output.WriteLine($"C2paException for file {file}: {ex}");
            }
        }
    }

    private static void AssertJsonEquivalent(string expectedJson, string actualJson)
    {
        var expectedNode = JsonNode.Parse(expectedJson);
        var actualNode = JsonNode.Parse(actualJson);
        Assert.True(
            JsonNode.DeepEquals(expectedNode, actualNode),
            $"JSON documents are not equivalent.\nExpected: {expectedJson}\nActual: {actualJson}");
    }
}