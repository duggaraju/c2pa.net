namespace Microsoft.ContentAuthenticity.Tests;

public class C2paExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var exception = new C2paException("Other", message);

        // Assert
        Assert.Equal(message, exception.Message);
    }
}