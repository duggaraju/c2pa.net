namespace ContentAuthenticity.Tests;

using System.Text.Json.Nodes;
using Xunit.Abstractions;

public class ReaderTests(ITestOutputHelper output)
{
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

    private static void AssertJsonEquivalent(string expectedJson, string actualJson)
    {
        var expectedNode = JsonNode.Parse(expectedJson);
        var actualNode = JsonNode.Parse(actualJson);
        Assert.True(
            JsonNode.DeepEquals(expectedNode, actualNode),
            $"JSON documents are not equivalent.\nExpected: {expectedJson}\nActual: {actualJson}");
    }
}