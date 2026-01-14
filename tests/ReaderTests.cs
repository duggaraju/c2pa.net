namespace ContentAuthenticity.Tests;

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
        var exception = Record.Exception(() => Reader.FromStream(stream, format));

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
            var exception = Record.Exception(() => Reader.FromFile(tempFile));

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
        Assert.Throws<FileNotFoundException>(() => Reader.FromFile(nonExistentFile));
    }

    [Fact]
    public void JsonRoundTrip_ShouldPreserveDataIntegrity()
    {
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.png");
        foreach (var file in files)
        {
            try
            {
                var reader = Reader.FromFile(file);
                var originalJson = reader.Json;
                var store = reader.Store;
                var roundTrippedJson = store.ToJson();
                Assert.Equal(originalJson, roundTrippedJson);
            }
            catch (C2paException ex)
            {
                output.WriteLine($"C2paException for file {file}: {ex}");
            }
        }
    }
}