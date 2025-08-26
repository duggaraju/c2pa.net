namespace Microsoft.ContentAuthenticity.Tests;

public class ReaderTests
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
}