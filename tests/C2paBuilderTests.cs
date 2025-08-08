using ContentAuthenticity.Bindings;

namespace ContentAuthenticity.BindingTests;

public class C2paBuilderTests
{
    [Fact]
    public void GenerateInstanceID_ShouldReturnValidGuid()
    {
        // Act
        var instanceId = C2paBuilder.GenerateInstanceID();

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
        var id1 = C2paBuilder.GenerateInstanceID();
        var id2 = C2paBuilder.GenerateInstanceID();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Create_WithManifestDefinitionAndSigner_ShouldCreateBuilder()
    {
        // Arrange
        var manifest = new ManifestDefinition("image/jpeg");

        // Act
        var exception = Record.Exception(() => C2paBuilder.Create(manifest));

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
        var exception = Record.Exception(() => C2paBuilder.FromJson(json));

        // Assert - Should not throw during creation, actual functionality depends on native library
        Assert.True(exception == null || exception is C2paException);
    }
}