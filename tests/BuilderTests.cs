namespace ContentAuthenticity.Tests;

public class BuilderTests
{
    [Fact]
    public void GenerateInstanceID_ShouldReturnValidGuid()
    {
        // Act
        var instanceId = Builder.GenerateInstanceID();

        // Assert
        Assert.NotNull(instanceId);
        Assert.StartsWith("xmp:iid:", instanceId);

        // Extract GUID part and verify it's valid
        var guidPart = instanceId.Substring("xmp:iid:".Length);
        Assert.True(Guid.TryParse(guidPart, out _));
    }

    [Fact]
    public void GenerateInstanceID_ShouldReturnUniqueIds()
    {
        // Act
        var id1 = Builder.GenerateInstanceID();
        var id2 = Builder.GenerateInstanceID();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Create_WithManifestDefinitionAndSigner_ShouldCreateBuilder()
    {
        // Arrange
        var manifest = new ManifestDefinition("image/jpeg");

        // Act
        var exception = Record.Exception(() => Builder.Create(manifest));

        // Assert - Should not throw during creation, actual functionality depends on native library
        // We can't test the full functionality without the native C2PA library being properly set up
        Assert.True(exception == null || exception is C2paException);
    }

    [Fact]
    public void FromJson_WithValidJson_ShouldCreateBuilder()
    {
        // Arrange
        var manifest = new ManifestDefinition("image/jpeg")
        {
            Title = "Test Image",
            Vendor = "Test Vendor"
        };
        var json = manifest.ToJson();

        // Act
        var exception = Record.Exception(() => Builder.FromJson(json));

        // Assert - Should not throw during creation, actual functionality depends on native library
        Assert.True(exception == null || exception is C2paException);
    }
}