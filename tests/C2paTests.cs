using ContentAuthenticity.Bindings;

namespace ContentAuthenticity.BindingTests;

public class C2paTests
{
    [Fact]
    public void Version_ShouldReturnNonEmptyString()
    {
        // Act
        var version = C2pa.Version;

        // Assert
        Assert.NotNull(version);
        Assert.NotEmpty(version);
    }

    [Fact]
    public void SupportedMimeTypes_ShouldReturnNonEmptyArray()
    {
        // Act
        var mimeTypes = C2pa.SupportedMimeTypes;

        // Assert
        Assert.NotNull(mimeTypes);
        Assert.NotEmpty(mimeTypes);
        Assert.Contains("image/jpeg", mimeTypes);
    }

    [Fact]
    public void CheckError_ShouldNotThrowWhenNoError()
    {
        // Act & Assert
        var exception = Record.Exception(() => C2pa.CheckError());
        Assert.Null(exception);
    }
}